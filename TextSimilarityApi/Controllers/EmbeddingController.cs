using Microsoft.AspNetCore.Mvc;
using TextSimilarityApi.Services;
using System.Linq;

namespace TextSimilarityApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EmbeddingController : ControllerBase
    {
        private readonly EmbeddingService _embeddingService;
        private readonly EmbeddingRepository _repository;

        public EmbeddingController(EmbeddingService embeddingService, EmbeddingRepository repository)
        {
            _embeddingService = embeddingService;
            _repository = repository;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Archivo no válido.");

            var filePath = Path.Combine("Uploads", file.FileName);
            Directory.CreateDirectory("Uploads");
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var text = TextExtractor.ExtractText(filePath);
            var embedding = await _embeddingService.GetEmbeddingAsync(text);
            _repository.SaveEmbedding(file.FileName, embedding, text);

            return Ok(new { message = $"Archivo '{file.FileName}' procesado y guardado." });
        }

        [HttpGet("download")]
        public IActionResult DownloadFile([FromQuery] string name)
        {
            // Buscar el archivo (ejemplo simplificado)
            var filePath = Path.Combine("Uploads", $"{name}");
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

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var forceDownload = Request.Query.ContainsKey("download") && Request.Query["download"].ToString().ToLower() == "true";
            if (forceDownload)
            {
                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{name}\"");
                return File(fileBytes, contentType);
            }
            else
            {
                return File(fileBytes, contentType);
            }
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
                //.Where(e => e.Similarity >= 0.6) //  Filtro por similitud mínima
                //.Take(5)
                .OrderByDescending(e => e.Similarity)
                .ToList()
                .FirstOrDefault();

            var _respuesta = await _embeddingService.GetRespuestaAsync(bestMatch?.Text, request.Query, bestMatch?.FileName);

            return Ok(new
            {
                results = _respuesta
                //results = bestMatch
                //best_match = bestMatch?.FileName,
                //similarity = bestMatch?.Similarity
            });
        }

        [HttpPost("searchweb")]
        public async Task<IActionResult> searchweb([FromBody] QueryRequest request)
        {

            var _respuesta = await _embeddingService.GetRespuestaWebAsync(request.Query);

            return Ok(new
            {
                results = _respuesta
                //results = bestMatch
                //best_match = bestMatch?.FileName,
                //similarity = bestMatch?.Similarity
            });
        }

        [HttpGet("documents")]
        public IActionResult GetDocuments()
        {
            var docs = _repository.GetAllDocuments();

            var result = docs.Select(d => new
            {
                d.FileName,
                d.Snippet,
                downloadUrl = Url.Action(
                    action: nameof(DownloadFile),
                    controller: "Embedding",
                    values: new { name = d.FileName },
                    protocol: Request.Scheme)
            });

            return Ok(result);
        }
    }

    public class QueryRequest
    {
        public string Query { get; set; }
    }
}

