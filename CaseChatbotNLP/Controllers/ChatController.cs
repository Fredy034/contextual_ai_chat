using Microsoft.AspNetCore.Mvc;
using CaseChatbotNLP.Services;
using Azure.Core;

namespace CaseChatbotNLP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly INLPService _nlpService;
        private readonly OpenAIService _openAIService;

        public ChatController(INLPService nlpService, OpenAIService openAIService)
        {
            _nlpService = nlpService;
            _openAIService = openAIService;
        }


        [HttpPost("ask")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest question)
        {
            var response = await _openAIService.GetChatResponseAsync(question);
            return Ok(new { question.Prompt, response });
        }


        [HttpPost("askchar")]
        public IActionResult AskQuestion([FromBody] string question)
        {
            var response = _nlpService.ProcessQuery(question);
            return Ok(new { question, response });

        }
    }

    public class ChatRequest
    {
        public string Prompt { get; set; }  // equivalente a "prompt" en el body
        public int tipo { get; set; }  // nuevo parámetro                                           
    }
}