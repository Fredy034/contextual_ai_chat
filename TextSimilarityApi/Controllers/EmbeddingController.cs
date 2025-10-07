using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.Formula.Functions;
using System.Text;
using TextSimilarityApi.Services;

namespace TextSimilarityApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EmbeddingController : ControllerBase
    {
        private readonly EmbeddingService _embeddingService;
        private readonly EmbeddingRepository _repository;
        private readonly IWebHostEnvironment _env;
        private readonly TextExtractor _textExtractor;
        private readonly InMemoryDocumentStore _memoryStore;
        private readonly VideoProcessor _videoProcessor;

        public EmbeddingController(
            EmbeddingService embeddingService,
            EmbeddingRepository repository,
            IWebHostEnvironment env,
            TextExtractor textExtractor,
            InMemoryDocumentStore memoryStore,
            VideoProcessor videoProcessor)
        {
            _embeddingService = embeddingService;
            _repository = repository;
            _env = env;
            _textExtractor = textExtractor;
            _memoryStore = memoryStore;
            _videoProcessor = videoProcessor;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? sessionId = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Archivo no válido.");

            var safeFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
            var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv" };

            // Guardamos en temp inicialmente (ya que lo necesitamos para procesar)
            string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{safeFileName}");
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                if (videoExtensions.Contains(extension))
                {
                    // --- Es video: procesar audio + frames ---
                    string filePathForProcessing = tempPath;
                    if (string.IsNullOrEmpty(sessionId))
                    {
                        var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads");
                        Directory.CreateDirectory(uploadsDir);
                        var destPath = Path.Combine(uploadsDir, safeFileName);

                        // Si ya existe un archivo con mismo nombre, agrega sufijo para no sobreescribir
                        if (System.IO.File.Exists(destPath))
                        {
                            var unique = $"{Path.GetFileNameWithoutExtension(safeFileName)}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(safeFileName)}";
                            destPath = Path.Combine(uploadsDir, unique);
                        }

                        System.IO.File.Move(tempPath, destPath);
                        filePathForProcessing = destPath;
                    }

                    // Llamo al VideoProcessor: devuelve segmentos (audio-chunks + OCR frames)
                    var segments = await _videoProcessor.ProcessVideoAsync(filePathForProcessing, safeFileName, fpsForFrames: 0.5, chunkWindowSeconds: 15, maxConcurrentEmbeddings: 4);

                    int savedCount = 0;
                    var savedSegments = new List<(string FileName, float[] Vector, string Text)>();
                    foreach (var seg in segments)
                    {
                        if (string.IsNullOrWhiteSpace(sessionId))
                        {
                            // Guardar permanentemente en DB
                            _repository.SaveEmbedding(seg.FileName, seg.Vector, seg.Text);
                            savedSegments.Add(seg);
                            savedCount++;
                        }
                        else
                        {
                            if (!_memoryStore.ContainsText(sessionId, seg.Text))
                            {
                                _memoryStore.Add(sessionId, seg.FileName, seg.Vector, seg.Text);
                                savedSegments.Add(seg);
                                savedCount++;
                            }
                        }
                    }

                    var audioParts = savedSegments
                        .Where(s => s.FileName != null && s.FileName.StartsWith("SEGMENT::AUDIO::"))
                        .Select(s => s.Text).ToList();

                    var frameParts = savedSegments
                        .Where(s => s.FileName != null && s.FileName.StartsWith("SEGMENT::FRAME::"))
                        .Select(s => s.Text).ToList();

                    var combinedBuilder = new StringBuilder();
                    if (audioParts.Any())
                    {
                        combinedBuilder.AppendLine("[Transcripción (audio)]");
                        combinedBuilder.AppendLine(string.Join("\n\n", audioParts.Distinct().Take(50)));
                        combinedBuilder.AppendLine();
                    }
                    if (frameParts.Any())
                    {
                        combinedBuilder.AppendLine("[Texto detectado en frames]");
                        combinedBuilder.AppendLine(string.Join("\n\n", frameParts.Distinct().Take(50)));
                        combinedBuilder.AppendLine();
                    }

                    var combinedText = combinedBuilder.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(combinedText))
                    {
                        combinedText = $"[Video procesado: {safeFileName}]";
                    }

                    if (string.IsNullOrEmpty(sessionId))
                    {
                        try
                        {
                            var summaryEmbedding = await _embeddingService.GetEmbeddingAsync(combinedText);
                            _repository.SaveEmbedding(safeFileName, summaryEmbedding, combinedText);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Upload] Warning: no se pudo guardar embedding summary para '{safeFileName}': {ex.Message}");
                        }
                    }

                    return Ok(new { message = $"Video '{safeFileName}' procesado. Segmentos procesados/guardados: {savedCount}." });
                }
                else
                {
                    // --- No es video: comportamiento original (documentos/imágenes) ---
                    // Si sessionId == null -> guardo en Uploads y en DB; si sessionId != null -> proceso en memoria temporal
                    if (string.IsNullOrEmpty(sessionId))
                    {
                        var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads");
                        Directory.CreateDirectory(uploadsDir);
                        var filePath = Path.Combine(uploadsDir, safeFileName);

                        if (System.IO.File.Exists(filePath))
                        {
                            var unique = $"{Path.GetFileNameWithoutExtension(safeFileName)}_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(safeFileName)}";
                            filePath = Path.Combine(uploadsDir, unique);
                        }

                        System.IO.File.Move(tempPath, filePath);

                        var extractedText = await _textExtractor.ExtractTextAsync(filePath);
                        var embedding = await _embeddingService.GetEmbeddingAsync(extractedText);
                        _repository.SaveEmbedding(Path.GetFileName(filePath), embedding, extractedText);

                        return Ok(new { message = $"Archivo '{Path.GetFileName(filePath)}' procesado y guardado." });
                    }
                    else
                    {
                        var extractedText = await _textExtractor.ExtractTextAsync(tempPath);

                        if (_memoryStore.ContainsText(sessionId, extractedText))
                        {
                            try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
                            return Ok(new { message = $"Archivo '{safeFileName}' ya existe en la sesión '{sessionId}' (duplicado ignorado)." });
                        }

                        var embedding = await _embeddingService.GetEmbeddingAsync(extractedText);
                        var added = _memoryStore.Add(sessionId, safeFileName, embedding, extractedText);

                        try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }

                        if (!added)
                        {
                            return Ok(new { message = $"Archivo '{safeFileName}' ya existe en la sesión '{sessionId}' (duplicado ignorado)." });
                        }

                        return Ok(new { message = $"Archivo '{safeFileName}' procesado y guardado en memoria para sesión '{sessionId}'." });
                    }
                }
            }
            catch (NotSupportedException ex)
            {
                try { if (!string.IsNullOrEmpty(tempPath) && System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Upload] Error procesando archivo: {ex}");
                try { if (!string.IsNullOrEmpty(tempPath) && System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
                return StatusCode(500, new { error = "Error procesando el archivo: " + ex.Message });
            }
        }


        [HttpGet("download")]
        public IActionResult DownloadFile([FromQuery] string name, [FromQuery] bool download = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Nombre de archivo requerido.");

            // Evitar path traversal y construir ruta absoluta
            var safeName = Path.GetFileName(name);
            var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads");
            var filePath = Path.Combine(uploadsDir, safeName);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".mkv" => "video/x-matroska",
                ".webm" => "video/webm",
                ".avi" => "video/x-msvideo",
                _ => "application/octet-stream"
            };

            if (download)
            {
                // Forzar descarga: PhysicalFile con fileDownloadName agrega Content-Disposition: attachment
                return PhysicalFile(filePath, contentType, safeName);
            }

            // Preview: devolver el archivo sin header de attachment (inline)
            return PhysicalFile(filePath, contentType);
        }

        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] QueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return BadRequest("Query is required.");

            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Query);
            var storedEmbeddings = _repository.GetAllEmbeddings();

            var sessionEmbeddings = request.SessionId != null ? _memoryStore.GetAllEmbeddings(request.SessionId) : new List<(string, float[], string)>();

            var allCandidates = new List<(string FileName, float[] Vector, string Text)>();
            allCandidates.AddRange(storedEmbeddings);
            allCandidates.AddRange(sessionEmbeddings);

            var bestMatch = allCandidates
                .Select(e => new
                {
                    e.FileName,
                    Similarity = e.Vector != null && e.Vector.Length > 0 ? SimilarityCalculator.CosineSimilarity(queryEmbedding, e.Vector) : -1.0,
                    e.Text
                })
                .OrderByDescending(e => e.Similarity)
                .FirstOrDefault();

            string historyText = request.History != null && request.History.Any()
                ? string.Join("\n---\n", request.History.Select(h => $"{h.Role.ToUpper()}: {h.Content}"))
                : string.Empty;
            var combined = $"{historyText}\n\nDocumento:\n{bestMatch?.Text ?? ""}";

            if (combined.Length > 16000) combined = combined.Substring(combined.Length - 16000);

            var _respuesta = await _embeddingService.GetRespuestaAsync(combined, request.Query, bestMatch?.FileName);

            return Ok(new { results = _respuesta });
        }

        [HttpPost("searchweb")]
        public async Task<IActionResult> searchweb([FromBody] QueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return BadRequest("Query is required.");

            // Construir historial como texto plano a partir de History
            string historyText = request.History != null && request.History.Any()
                ? string.Join("\n---\n", request.History.Select(h => $"{h.Role.ToUpper()}: {h.Content}"))
                : string.Empty;

            var sessionDocs = new List<(string FileName, float[] Vector, string Text)>();
            if (!string.IsNullOrEmpty(request.SessionId))
            {
                sessionDocs = _memoryStore.GetAllEmbeddings(request.SessionId) ?? new List<(string, float[], string)>();
            }

            string sessionText = string.Empty;
            List<string> includedDocNames = new();
            if (sessionDocs != null && sessionDocs.Any())
            {
                sessionText = string.Join("\n---\n", sessionDocs.Select(d =>
                {
                    includedDocNames.Add(d.FileName ?? "sin-nombre");
                    return $"Documento: {d.FileName}\n{d.Text}";
                }));
            }

            var combinedBuilder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(historyText))
            {
                combinedBuilder.AppendLine(historyText);
                combinedBuilder.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(sessionText))
            {
                combinedBuilder.AppendLine("Documentos de sesión:");
                combinedBuilder.AppendLine(sessionText);
                combinedBuilder.AppendLine();
            }
            combinedBuilder.AppendLine("Consulta web:");
            combinedBuilder.AppendLine(request.Query);

            var combined = combinedBuilder.ToString();

            if (combined.Length > 16000) combined = combined.Substring(combined.Length - 16000);

            string webResult = string.Empty;
            try
            {
                webResult = await _embeddingService.GetRespuestaWebAsync(combined) ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[searchweb] Error al obtener webResult: {ex}");
                webResult = "";
            }

            return Ok(new { results = webResult, sessionDocuments = includedDocNames.Distinct().ToList() });
        }

        [HttpGet("documents")]
        public IActionResult GetDocuments()
        {
            var docs = _repository.GetAllDocuments();

            var result = docs.Select(d =>
            {
                // Construir URLs absolutas: preview (inline) y download (forzada)
                var previewUrl = Url.Action(
                    action: nameof(DownloadFile),
                    controller: "Embedding",
                    values: new { name = d.FileName, download = false },
                    protocol: Request.Scheme);

                var downloadUrl = Url.Action(
                    action: nameof(DownloadFile),
                    controller: "Embedding",
                    values: new { name = d.FileName, download = true },
                    protocol: Request.Scheme);

                return new
                {
                    d.FileName,
                    d.Snippet,
                    previewUrl,
                    downloadUrl
                };
            });

            return Ok(result);
        }

        [HttpGet("documents/session")]
        public IActionResult GetSessionDocuments([FromQuery] string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("SessionId is required.");
            var docs = _memoryStore.GetDocuments(sessionId);
            var result = docs.Select(d => new
            {
                FileName = d.FileName,
                Snippet = d.Snippet,
                previewUrl = (string?)null,
                downloadUrl = (string?)null
            });

            return Ok(result);
        }
    }

    public class ChatMessageDTO
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class QueryRequest
    {
        public string Query { get; set; } = String.Empty;
        public List<ChatMessageDTO> History { get; set; } = new();
        public string? SessionId { get; set; }
    }
}
