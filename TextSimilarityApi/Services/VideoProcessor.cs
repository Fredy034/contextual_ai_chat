using NAudio.Wave;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vosk;

namespace TextSimilarityApi.Services
{
    public class VideoProcessor
    {
        private readonly EmbeddingService _embeddingService;
        private readonly TextExtractor _textExtractor;
        private readonly string _ffmpegPath;
        private readonly Model _voskModel;

        public VideoProcessor(EmbeddingService embeddingService, TextExtractor textExtractor, string ffmpegPath, string voskModelPath)
        {
            _embeddingService = embeddingService;
            _textExtractor = textExtractor;
            _ffmpegPath = ResolveFfmpegPath(ffmpegPath);
            
            Vosk.Vosk.SetLogLevel(0);
            if (string.IsNullOrWhiteSpace(voskModelPath)) throw new ArgumentException("Vosk model path must be provided.", nameof(voskModelPath));
            _voskModel = new Model(voskModelPath);
        }

        public async Task<List<(string FileName, float[] Vector, string Text)>> ProcessVideoAsync(
            string videoPath, 
            string originalFileName, 
            double fpsForFrames = 1.0,          // Permitir valores fraccionarios
            int chunkWindowSeconds = 15,        // Ventana de chunking para transcripción
            int maxConcurrentEmbeddings = 4,    // Máximo de llamadas concurrentes a embedding
            int minOcrChars = 10,               // Mínimo de caracteres OCR para considerar
            double minOcrConfidence = 0.40)     // Mínima confianza OCR para considerar
        {
            var results = new List<(string, float[], string)>();

            // 1. Extract frames from video
            var wavPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");
            ExtractAudioToWav(videoPath, wavPath);

            // 2. Transcribe audio to text
            var wordResults = await TranscribeWithVoskAsync(wavPath);

            // 3. Chunk text based on time windows
            var transcriptChunks = BuildTimeWindowChunks(wordResults, chunkWindowSeconds);

            var audioTask = new List<Task<(string FileName, float[] vector, string Text)>>();

            // 4. For each chunk, get embedding and store
            int segmentIndex = 0;
            foreach (var chunk in transcriptChunks)
            {
                var segFileName = $"SEGMENT::AUDIO::{originalFileName}::segment:{segmentIndex}::{chunk.Start:F2}-{chunk.End:F2}";
                if (string.IsNullOrWhiteSpace(chunk.Text) || chunk.Text.Length < 20)
                {
                    segmentIndex++;
                    continue;
                }

                audioTask.Add(Task.Run(async () =>
                {
                   var vector = await _embeddingService.GetEmbeddingAsync(chunk.Text);
                    return (segFileName, vector, $"[Audio {TimeSpan.FromSeconds(chunk.Start):hh\\:mm\\:ss} - {TimeSpan.FromSeconds(chunk.End):hh\\:mm\\:ss}]\n{chunk.Text}");
                }));

                segmentIndex++;
            }

            // 5. Extract text from key frames
            var frameDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_frames");
            Directory.CreateDirectory(frameDir);
            ExtractFrames(videoPath, frameDir, fpsForFrames);

            // Process each frame image
            var frameFiles = Directory.GetFiles(frameDir, "*.png").OrderBy(f => f).ToList();
            
            int maxFrames = 120;
            if (frameFiles.Count > maxFrames)
                frameFiles = frameFiles.Take(maxFrames).ToList();

            var seenHashes = new HashSet<string>();
            var frameTasks = new List<Task<(string FileName, float[] vector, string Text)>>();

            foreach (var framePath in frameFiles)
            {
                frameTasks.Add(Task.Run(async () =>
                {
                    var raw = await _textExtractor.ExtractTextFromImageFileAsync(framePath);
                    ParseOcrOutput(raw, out string cleanText, out double confidence);

                    if (string.IsNullOrWhiteSpace(cleanText) || cleanText.Length < minOcrChars || confidence < minOcrConfidence) return (null, (float[])null, (string)null);

                    var normalizedHash = NormalizeForHash(cleanText);
                    using var sha256 = SHA256.Create();
                    var hashBytes = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedHash)));
                    lock (seenHashes)
                    {
                        if (seenHashes.Contains(hashBytes)) return (null, (float[])null, (string)null);
                        seenHashes.Add(hashBytes);
                    }

                    var frameFileName = Path.GetFileName(framePath);
                    var idxStr = Regex.Match(frameFileName, @"\d+").Value;
                    var idx = 0;
                    int.TryParse(idxStr, out idx);
                    var tsSeconds = idx / Math.Max(1.0, fpsForFrames);
                    var tsLabel = TimeSpan.FromSeconds(tsSeconds).ToString(@"hh\:mm\:ss");
                    var segFileName = $"SEGMENT::FRAME::{originalFileName}::frame:{tsLabel}::{frameFileName}";

                    var vector = await _embeddingService.GetEmbeddingAsync(cleanText);
                    return (segFileName, vector, cleanText);
                }));
            }

            var allTasks = audioTask.Concat(frameTasks).ToList();

            var semaphore = new SemaphoreSlim(maxConcurrentEmbeddings);
            var guarded = allTasks.Select(task => Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await task;
                }
                finally { semaphore.Release(); }
            })).ToList();

            var completed = await Task.WhenAll(guarded);

            foreach (var item in completed)
            {
                if (item.Item1 != null && item.Item2 != null && !string.IsNullOrWhiteSpace(item.Item3))
                    results.Add((item.Item1, item.Item2, item.Item3));
            }

            // Cleanup temp files
            try { if (File.Exists(wavPath)) File.Delete(wavPath); } catch { }
            try { if (Directory.Exists(frameDir)) Directory.Delete(frameDir, true); } catch { }

            return results;
        }

        private void ParseOcrOutput(string raw, out string cleanText, out double confidence)
        {
            cleanText = string.Empty;
            confidence = 0.0;

            if (string.IsNullOrWhiteSpace(raw)) return;

            var confMatch = Regex.Match(raw, @"OCR Confidence:\s*([0-9\.,]+)");
            if (confMatch.Success)
            {
                var s = confMatch.Groups[1].Value.Replace(',', '.');
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var c)) confidence = c;
            }

            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (line.StartsWith("[OCR extraído", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("[OCR Confidence", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("[Imagen", StringComparison.OrdinalIgnoreCase)) continue;
                // skip metadata-like lines
                sb.AppendLine(line);
            }
            cleanText = sb.ToString().Trim();
        }

        private string NormalizeForHash(string s)
        {
            var t = Regex.Replace(s.ToLowerInvariant(), @"\p{P}+", ""); // remove punctuation
            t = Regex.Replace(t, @"\s+", " ").Trim();
            return t;
        }

        private string ResolveFfmpegPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) configuredPath = "ffmpeg";
            if (Directory.Exists(configuredPath))
            {
                var candidate = Path.Combine(configuredPath, "bin", "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
                candidate = Path.Combine(configuredPath, "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            if (File.Exists(configuredPath))
                return configuredPath;
            if (File.Exists(configuredPath + ".exe"))
                return configuredPath + ".exe";

            return configuredPath;
        }

        private void EnsureFfmpegAvailable()
        {
            var exe = ResolveFfmpegPath(_ffmpegPath);

            try
            {
                var check = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "-version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(check);
                p.WaitForExit(3000);
                if (p.HasExited && p.ExitCode == 0) return;
            }
            catch { }

            throw new FileNotFoundException($"ffmpeg ejecutable no encontrado en '{exe}'. Asegúrate que ffmpeg esté instalado y la ruta apunte al ejecutable (ej: C:\\ffmpeg\\bin\\ffmpeg.exe) o que 'ffmpeg' esté en el PATH.");
        }

        private void ExtractAudioToWav(string videoPath, string wavOutput, int timeoutMs = 120000)
        {
            EnsureFfmpegAvailable();

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -i \"{videoPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{wavOutput}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var stderr = new StringBuilder();
            var stdout = new StringBuilder();

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            p.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            if (!p.Start())
                throw new Exception("No se pudo iniciar ffmpeg para extraer audio.");

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(true); } catch { }
                throw new TimeoutException($"ffmpeg (audio) excedió el timeout de {timeoutMs / 1000}s. StdErr: {stderr.ToString()}");
            }

            p.WaitForExit();
            var errText = stderr.ToString();
            var outText = stdout.ToString();

            if (p.ExitCode != 0)
            {
                throw new Exception($"FFmpeg audio extraction failed (exit {p.ExitCode}). StdErr: {errText}");
            }

            if (!File.Exists(wavOutput))
                throw new FileNotFoundException("ffmpeg no generó el archivo WAV esperado. StdErr: " + errText);
        }

        private void ExtractFrames(string videoPath, string outputDir, double fps, int timeoutMs = 120000)
        {
            EnsureFfmpegAvailable();

            var fpsStr = fps.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var outPattern = Path.Combine(outputDir, "frame_%06d.png");

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -i \"{videoPath}\" -vf \"fps={fpsStr}\" \"{outPattern}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var stderr = new StringBuilder();
            var stdout = new StringBuilder();

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            p.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            if (!p.Start())
                throw new Exception("No se pudo iniciar ffmpeg para extraer frames.");

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(true); } catch { }
                throw new TimeoutException($"ffmpeg (frames) excedió el timeout de {timeoutMs / 1000}s. StdErr: {stderr.ToString()}");
            }

            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                throw new Exception($"ffmpeg frames extraction failed (exit {p.ExitCode}). StdErr: {stderr.ToString()}");
            }
        }


        private async Task<List<VoskWord>> TranscribeWithVoskAsync(string wavPath)
        {
            var words = new List<VoskWord>();
            using var waveReader = new WaveFileReader(wavPath);
            using var recognizer = new VoskRecognizer(_voskModel, (float)waveReader.WaveFormat.SampleRate);
            byte[] buffer = new byte[4096];
            int read;
            while ((read = waveReader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (recognizer.AcceptWaveform(buffer, read))
                {
                    var res = recognizer.Result();
                    ParseVoskResultIntoWords(res, words);
                }
            }
            
            var final = recognizer.FinalResult();
            ParseVoskResultIntoWords(final, words);

            return words;
        }

        private void ParseVoskResultIntoWords(string jsonResult, List<VoskWord> outWords)
        {
            if (string.IsNullOrWhiteSpace(jsonResult)) return;
            try
            {
                using var doc = JsonDocument.Parse(jsonResult);
                if (doc.RootElement.TryGetProperty("result", out var resultArr))
                {
                    foreach (var el in resultArr.EnumerateArray())
                    {
                        var w = new VoskWord
                        {
                            Word = el.GetProperty("word").GetString() ?? "",
                            Start = el.GetProperty("start").GetDouble(),
                            End = el.GetProperty("end").GetDouble()
                        };
                        outWords.Add(w);
                    }
                }
                else if (doc.RootElement.TryGetProperty("text", out var textElm))
                {
                    var t = textElm.GetString() ?? "";
                    outWords.Add(new VoskWord { Word = t, Start = 0, End = 0 });
                }
            }
            catch { }
        }

        private List<TranscriptChunk> BuildTimeWindowChunks(List<VoskWord> words, int windowSeconds)
        {
            var chunks = new List<TranscriptChunk>();
            if (words == null || words.Count == 0) return chunks;

            var currentStart = words[0].Start;
            var currentEnd = currentStart + windowSeconds;
            var sb = new StringBuilder();

            foreach (var w in words)
            {
                if (w.Start <= currentEnd)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(w.Word);
                    if (w.End > currentEnd) currentEnd = w.End;
                }
                else
                {
                    var text = sb.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        chunks.Add(new TranscriptChunk { Start = currentStart, End = currentEnd, Text = text });

                    currentStart = w.Start;
                    currentEnd = currentStart + windowSeconds;
                    sb.Clear();
                    sb.Append(w.Word);
                }
            }

            var lastText = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(lastText))
                chunks.Add(new TranscriptChunk { Start = currentStart, End = currentEnd, Text = lastText });

            return chunks;
        }

        private class VoskWord
        {
            public string Word { get; set; } = "";
            public double Start { get; set; }
            public double End { get; set; }
        }

        private class TranscriptChunk
        {
            public double Start { get; set; }
            public double End { get; set; }
            public string Text { get; set; } = "";
        }
    }
}
