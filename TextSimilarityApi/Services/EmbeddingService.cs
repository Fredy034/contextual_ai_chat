using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.Json;

namespace TextSimilarityApi.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _client;
        private readonly string _deployment;
        private readonly string _apiVersion;

        public EmbeddingService(IConfiguration config)
        {
            var endpoint = config["AzureOpenAI:Endpoint"];
            var apiKey = config["AzureOpenAI:ApiKey"];
            _deployment = config["AzureOpenAI:Deployment"];
            _apiVersion = config["AzureOpenAI:ApiVersion"];

            _client = new HttpClient { BaseAddress = new Uri(endpoint) };
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<float[]> GetEmbeddingAsync(string input)
        {
            var requestBody = new { input };
            var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync($"/openai/deployments/{_deployment}/embeddings?api-version={_apiVersion}", content);
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());

            return json["data"]?[0]?["embedding"]?.Select(v => (float)v).ToArray() ?? Array.Empty<float>();
        }

        public async Task<string> GetRespuestaAsync(string texto, string question, string filename)
        {
            var contextText = string.Join("\n---\n", texto);

            var deployment = "gpt-4.1-nano";
            var apiVersion = "2025-01-01-preview";

            var systemPrompt = "Responde la pregunta solo con base en el contexto.";

            var userMessage = $@"
                Eres un asistente que responde preguntas basadas en texto.
                Contexto: {contextText}
                Pregunta: {question}
            ";

            var requestBody = new {
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt  },
                    new { role = "user", content = userMessage }
                },
                temperature = 0
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync($"/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var answer = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            // string _documento = $"<br> <a target=\"_blank\" href=\"https://ch-npl-d5djc6cafehnfgf7.eastus-01.azurewebsites.net/Embedding/download?name={filename}&download=false\" > Ver documento</a>";
            string _documento = $"<br> <a target=\"_blank\" href=\"https://localhost:7180/Embedding/download?name={filename}&download=false\" > Ver documento</a>";
            if (question.ToLower().Contains("hola"))
                _documento = "";
            if (answer != null && answer.Contains("No hay información suficiente"))
                _documento = "";

            return (answer?.Replace("\n", "<br>") ?? "") + _documento;
        }

        public async Task<string> GetRespuestaWebAsync(string question)
        {

            var deployment = "gpt-4.1-nano";
            var apiVersion = "2025-01-01-preview";

            var finalPrompt = $@"
            Eres un asistente que responde preguntas.
           
            Pregunta: {question}";

            var requestBody = new
            {
                messages = new[]
            {
             new { role = "system", content = "" },
             new { role = "user", content = finalPrompt }

             },
                temperature = 0
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync($"/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
           
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString().Replace("\n","<br>") + "<br>";
        }
    }
}
