using Microsoft.AspNetCore.Mvc;
using TextSimilarityApi.Services;

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

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/octet-stream", $"{name}");
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
    }



    public class QueryRequest
    {
        public string Query { get; set; }
    }
}

