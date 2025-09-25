using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.Formula.Functions;
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

        public EmbeddingController(EmbeddingService embeddingService, EmbeddingRepository repository, IWebHostEnvironment env, TextExtractor textExtractor, InMemoryDocumentStore memoryStore)
        {
            _embeddingService = embeddingService;
            _repository = repository;
            _env = env;
            _textExtractor = textExtractor;
            _memoryStore = memoryStore;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? sessionId = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Archivo no válido.");

            var safeFileName = Path.GetFileName(file.FileName);

            string extractedText = string.Empty;
            string tempPath = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads");
                    Directory.CreateDirectory(uploadsDir);
                    var filePath = Path.Combine(uploadsDir, safeFileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    extractedText = await _textExtractor.ExtractTextAsync(filePath);
                    var embedding = await _embeddingService.GetEmbeddingAsync(extractedText);
                    _repository.SaveEmbedding(safeFileName, embedding, extractedText);

                    return Ok(new { message = $"Archivo '{safeFileName}' procesado y guardado." });
                } 
                else
                {
                    tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{safeFileName}");
                    using (var stream = new FileStream(tempPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    extractedText = await _textExtractor.ExtractTextAsync(tempPath);
                    var embedding = await _embeddingService.GetEmbeddingAsync(extractedText);

                    // Guardar en memoria
                    _memoryStore.Add(sessionId, safeFileName, embedding, extractedText);

                    // Eliminar archivo temporal
                    try { System.IO.File.Delete(tempPath); }
                    catch { /* Ignorar errores al eliminar temp */ }

                    return Ok(new { message = $"Archivo '{safeFileName}' procesado y guardado en memoria para sesión '{sessionId}'." });
                }
            }
            catch (NotSupportedException ex)
            {
                if (!string.IsNullOrEmpty(tempPath) && System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Upload] Error extrayendo texto: {ex}");
                if (!string.IsNullOrEmpty(tempPath) && System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
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
                    Similarity = e.Vector != null && e.Vector.Length > 0 ?  SimilarityCalculator.CosineSimilarity(queryEmbedding, e.Vector) : -1.0,
                    e.Text
                })
                .OrderByDescending(e => e.Similarity)
                .FirstOrDefault();

            string historyText = request.HistoryText ?? string.Join("\n---\n", request.History.Select(h => $"{h.Role.ToUpper()}: {h.Content}"));
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

            var webResult = string.Empty;

            string historyText = request.HistoryText ?? (request.History != null && request.History.Any() ? string.Join("\n---\n", request.History.Select(h => $"{h.Role.ToUpper()}: {h.Content}")) : string.Empty);
            if (historyText.Length > 16000) historyText = historyText.Substring(historyText.Length - 16000);

            try
            {
                webResult = await _embeddingService.GetRespuestaWebAsync(historyText) ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[searchweb] Error al obtener webResult: {ex}");
                webResult = "";
            }

            return Ok(new { results = webResult });
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
            var result = docs.Select(d => new { 
                FileName = d.FileName,
                Snippet = d.Snippet,
                previewUrl = (string?)null,
                downloadUrl = (string?)null
            });

            return Ok(result);
        }
    }

    public class ChatMessageDTO {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class QueryRequest
    {
        public string Query { get; set; } = String.Empty;
        public List<ChatMessageDTO> History { get; set; } = new();
        public string? HistoryText { get; set; }
        public string? SessionId { get; set; }
    }
}
