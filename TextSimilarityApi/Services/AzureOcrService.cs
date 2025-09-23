using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System.Text;

namespace TextSimilarityApi.Services
{
    public class AzureOcrService
    {
        private readonly ComputerVisionClient _client;

        public AzureOcrService(IConfiguration config)
        {
            var endpoint = config["AzureOpenAI:Endpoint"];
            var key = config["AzureOpenAI:ApiKey"];
            _client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
            {
                Endpoint = endpoint
            };
        }

        public async Task<string> ReadTextFromFileAsync(string filePath, string language = "es")
        {
            using var stream = File.OpenRead(filePath);

            var readOp = await _client.ReadInStreamAsync(stream, language: language);
            var operationLocation = readOp.OperationLocation;
            var operationId = operationLocation.Split('/').Last();

            ReadOperationResult results = null;
            int maxRetries = 60;
            int retry = 0;
            int delay = 1000;

            do
            {
                await Task.Delay(delay);
                results = await _client.GetReadResultAsync(Guid.Parse(operationId));
                if (results.Status == OperationStatusCodes.Succeeded || results.Status == OperationStatusCodes.Failed)
                    break;

                retry++;
            } while (retry < maxRetries);

            if (results == null || results.Status != OperationStatusCodes.Succeeded)
                throw new Exception($"Azure Read API no finalizó correctamente. Estado: {results?.Status}");

            var sb = new StringBuilder();

            foreach (var page in results.AnalyzeResult.ReadResults)
            {
                foreach (var line in page.Lines)
                {
                    sb.AppendLine(line.Text);
                }
            }

            return sb.ToString();
        }
    }
}
