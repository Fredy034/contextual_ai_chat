using Microsoft.AspNetCore.Mvc;
using TextSimilarityApi.Services;
using Microsoft.AspNetCore.Hosting;

namespace TextSimilarityApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EmbeddingController : ControllerBase
    {
        private readonly EmbeddingService _embeddingService;
        private readonly EmbeddingRepository _repository;
        private readonly IWebHostEnvironment _env;

        public EmbeddingController(EmbeddingService embeddingService, EmbeddingRepository repository, IWebHostEnvironment env)
        {
            _embeddingService = embeddingService;
            _repository = repository;
            _env = env;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Archivo no válido.");

            // Guardar en ruta absoluta dentro del ContentRootPath
            var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads");
            Directory.CreateDirectory(uploadsDir);

            var safeFileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploadsDir, safeFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            string text;
            try
            {
                text = TextExtractor.ExtractText(filePath);
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Upload] Error extrayendo texto: {ex}");
                return StatusCode(500, new { error = "Error procesando el archivo: " + ex.Message });
            }

            var embedding = await _embeddingService.GetEmbeddingAsync(text);
            _repository.SaveEmbedding(safeFileName, embedding, text);

            return Ok(new { message = $"Archivo '{safeFileName}' procesado y guardado." });
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
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Query);
            var storedEmbeddings = _repository.GetAllEmbeddings();

            var bestMatch = storedEmbeddings
                .Select(e => new
                {
                    e.FileName,
                    Similarity = SimilarityCalculator.CosineSimilarity(queryEmbedding, e.Vector),
                    e.Text
                })
                .OrderByDescending(e => e.Similarity)
                .ToList()
                .FirstOrDefault();

            var _respuesta = await _embeddingService.GetRespuestaAsync(bestMatch?.Text, request.Query, bestMatch?.FileName);

            return Ok(new
            {
                results = _respuesta
            });
        }

        [HttpPost("searchweb")]
        public async Task<IActionResult> searchweb([FromBody] QueryRequest request)
        {
            var _respuesta = await _embeddingService.GetRespuestaWebAsync(request.Query);

            return Ok(new
            {
                results = _respuesta
            });
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
    }

    public class QueryRequest
    {
        public string Query { get; set; }
    }
}
