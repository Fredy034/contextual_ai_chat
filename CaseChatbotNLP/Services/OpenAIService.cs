

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CaseChatbotNLP.Controllers;

namespace CaseChatbotNLP.Services
{

    public class OpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly QueryExecutor _queryExecutor;

        public OpenAIService(HttpClient httpClient, IConfiguration config, QueryExecutor queryExecutor)
        {
            _httpClient = httpClient;
            _config = config;
            _queryExecutor = queryExecutor;
        }

        public async Task<string> GetChatResponseAsync(ChatRequest question)
        {
            var endpoint = _config["AzureOpenAI:Endpoint"];
            var apiKey = _config["AzureOpenAI:ApiKey"];
            var deployment = _config["AzureOpenAI:Deployment"];
            var apiVersion = "2025-01-01-preview";

            _httpClient.BaseAddress = new Uri(endpoint);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                messages = new[]
            {
             new { role = "system", content = "Eres un asistente útil." },
             new { role = "user", content = "Eres un asistente que responde preguntas sobre la tabla SQL Server \"getReporteGenerico()\". La tabla tiene las columnas Prefijo(id del caso), Descripcion(buscar en este campo con un like), Estado(Asignado,Latonero,Pintura,Mecánica,Calidad Latonero,Entrega,Finalizado,Calidad Pintura,Calidad Mecánica), FechaCreacion, FechaEstimada, Responsable(Buscar en este campo por un like), Placa(Placa del vehiculo, buscar por un like). Pregunta: " + "'" + question.Prompt + "'" + "Genera la consulta SQL sever que responde esta pregunta."}
             //new { role = "user", content = "Eres un asistente que responde preguntas sobre la tabla SQL Server \\\"vista_casos\\\". La tabla tiene las columnas Prefijo(id del caso), Descripcion(Buscar en este campo con un like), Estado(Abierto, Pendiente, Cerrado, Solucionado, Rechazado), FechaCreacion(Fecha en que se crea el caso), FechaEstimada(Fecha posible o fecha estimada o fecha propuesta de solucion), FechaSolucionado(La fecha de solucion del caso),NombreCentro(Centro de trabajo o cedi,NombreServicio(categoria o servicio o oferta, buscar en este campo con un like), buscar en este campo por un like) ,NombreGrupoResolutor(Equipo o grupo de trabajo que atiende el caso, Buscar en este campo por un like) Responsable(persona que atiende el caso, Buscar en este campo por un like),  Vencido (Vegente equivale a un caso no vencido, vencido esquivale a casos vencidos). si hacen preguntas como \"casos que se vencen hoy, proximas casos a vencer\" tener encuenta que el campo vencido sea vigente y la fecha estimada de solución \r\n Pregunta: " + "'" + question.Prompt + "'" + "Genera la consulta SQL sever que responde esta pregunta."}
             },
                temperature = 0.7,
                max_tokens = 500
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var jsonresult = _queryExecutor.ExecuteQuery(ExtractSqlQuery(doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString())).Replace("'", "\""); // Corrige comillas

            if (jsonresult == "")
                jsonresult = "[{ \"Respuesta\": \"No se encontro información, agregue un poco mas de detalle para mejorar la consulta \"}]";
            
            var data = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonresult);

            var sb = new StringBuilder();
            sb.AppendLine("<table border='1' class='tabladatos'>");

            // Encabezados (tomados del primer elemento)
            if (data != null && data.Count > 0)
            {
                sb.AppendLine("<thead><tr>");
                foreach (var header in data[0].Keys)
                {
                    sb.AppendLine($"<th>{header}</th>");
                }
                sb.AppendLine("</tr></thead>");
            }

            // Filas
            sb.AppendLine("<tbody>");
            foreach (var row in data)
            {
                sb.AppendLine("<tr>");
                foreach (var cell in row.Values)
                {
                    sb.AppendLine($"<td>{cell}</td>");
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
            if (question.tipo == 0)
            {
                return sb.ToString();
            }
            else
            {
                var finalPrompt = $@"
                Eres un asistente que responde preguntas basadas en texto.

                Contexto: a continuación encontraras una tabla html que tiene información seleccionada de una base de datos que da respuesta a la pregunta, pero se requere que sea una respuesta en lenguaje natural
                {sb.ToString()}

                Pregunta: {question.Prompt}";

                var requestBodyRes = new
                {
                    messages = new[]
                    {
                     new { role = "system", content = "Responde la pregunta solo con base en el contexto." },
                     new { role = "user", content = finalPrompt }

                     },
                     temperature = 0
                };

                content = new StringContent(JsonSerializer.Serialize(requestBodyRes), Encoding.UTF8, "application/json");

                response = await _httpClient.PostAsync($"/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}", content);
                response.EnsureSuccessStatusCode();

                json = await response.Content.ReadAsStringAsync();
                using var docres = JsonDocument.Parse(json);
                
                return docres.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            }
        }

        public string ExtractSqlQuery(string input)
        {
            // Patrón para capturar contenido entre ```sql y ```
            var pattern = @"```sql\s*(.*?)\s*```";
            var match = Regex.Match(input, pattern, RegexOptions.Singleline);

            return match.Success ? match.Groups[1].Value.Trim() : input;
        }

    }
}
