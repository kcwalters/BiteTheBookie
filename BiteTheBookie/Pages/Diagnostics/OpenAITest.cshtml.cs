using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace BiteTheBookie.Pages.Diagnostics
{
    public class OpenAITestModel : PageModel
    {
        private readonly ChatClient? _chatClient;
        private readonly ILogger<OpenAITestModel> _logger;

        public string ResultMessage { get; private set; } = string.Empty;

        public OpenAITestModel(ChatClient? chatClient, ILogger<OpenAITestModel> logger)
        {
            _chatClient = chatClient;
            _logger = logger;
        }

        public async Task OnGetAsync()
        {
            if (_chatClient == null)
            {
                ResultMessage = "ChatClient is not configured in this environment.";
                _logger.LogWarning(ResultMessage);
                return;
            }

            try
            {
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a simple connectivity test. Reply with OK."),
                    new UserChatMessage("Connectivity test: please reply with the single token 'OK'.")
                };

                var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions { Temperature = 0.0f });
                var text = response.Value.Content[0].Text ?? string.Empty;

                // Log a preview but do not expose sensitive content in logs beyond what the API returns
                _logger.LogInformation("OpenAI connectivity test succeeded. Response preview: {Preview}",
                    text.Length > 500 ? text[..500] + "..." : text);

                ResultMessage = "OpenAI responded: " + (text.Length > 1000 ? text[..1000] + "..." : text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI connectivity test failed");
                ResultMessage = "OpenAI connectivity test failed: " + ex.Message;
            }
        }
    }
}
