using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class SpreadAnalysisService : ISpreadAnalysisService
    {
        private readonly ILogger<SpreadAnalysisService> _logger;
        private readonly ChatClient? _chatClient;

        public SpreadAnalysisService(IConfiguration configuration, ILogger<SpreadAnalysisService> logger)
        {
            _logger = logger;

            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var apiKey = configuration["AzureOpenAI:ApiKey"];
            var deploymentName = configuration["AzureOpenAI:DeploymentName"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("Azure OpenAI configuration is missing. Will use basic analysis.");
                _chatClient = null;
            }
            else
            {
                var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                _chatClient = azureClient.GetChatClient(deploymentName);
            }
        }

        public async Task<List<SpreadOpportunity>> AnalyzeSpreadOpportunitiesAsync(List<NBAGameMatchup> games, CancellationToken cancellationToken = default)
        {
            var opportunities = new List<SpreadOpportunity>();

            if (_chatClient == null)
            {
                _logger.LogInformation("Using basic spread analysis (no AI)");
                return GetBasicAnalysis(games);
            }

            try
            {
                _logger.LogInformation("Analyzing {Count} games for spread opportunities using AI", games.Count);

                foreach (var game in games)
                {
                    var opportunity = await AnalyzeGameSpreadAsync(game, cancellationToken);
                    if (opportunity != null)
                    {
                        opportunities.Add(opportunity);
                    }
                }

                // Sort by confidence (highest first)
                return opportunities.OrderByDescending(o => o.Confidence).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing spread opportunities with AI, using basic analysis");
                return GetBasicAnalysis(games);
            }
        }

        private async Task<SpreadOpportunity?> AnalyzeGameSpreadAsync(NBAGameMatchup game, CancellationToken cancellationToken)
        {
            try
            {
                var prompt = $@"Analyze this NBA game for CONTRARIAN betting opportunities (going against the spread):

**Game:** {game.AwayTeamName} @ {game.HomeTeamName}
**Spread:** {(game.Spread > 0 ? "+" : "")}{game.Spread} (home team perspective)
**Over/Under:** {game.OverUnder}

As a contrarian betting analyst, identify if there's value in betting AGAINST the popular opinion. Consider:
1. Is the spread overvalued or undervalued?
2. Which team offers better value against the spread?
3. Statistical advantages that the market might be overlooking
4. Historical performance in similar situations

Return ONLY a JSON object with this structure (no markdown, no explanations):
{{
  ""recommendedBet"": ""Take Home"" or ""Take Away"",
  ""confidence"": 0-100,
  ""reasoning"": ""Brief explanation of why this is a good contrarian bet"",
  ""statisticalEdges"": [""edge 1"", ""edge 2"", ""edge 3""],
  ""valueRating"": ""High Value"" or ""Medium Value"" or ""Low Value""
}}

Only recommend bets with confidence >= 60. If confidence is below 60, return null.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an expert NBA betting analyst specializing in contrarian bets and finding value against the spread. Return only valid JSON."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions
                {
                    Temperature = 0.7f
                };

                var response = await _chatClient!.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var content = response.Value.Content[0].Text.Trim();

                // Clean up response
                if (content.StartsWith("```json")) content = content.Substring(7);
                if (content.StartsWith("```")) content = content.Substring(3);
                if (content.EndsWith("```")) content = content.Substring(0, content.Length - 3);
                content = content.Trim();

                if (content.ToLower() == "null") return null;

                var analysis = JsonSerializer.Deserialize<AISpreadAnalysis>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (analysis == null || analysis.Confidence < 60) return null;

                return new SpreadOpportunity
                {
                    Game = game,
                    RecommendedBet = analysis.RecommendedBet,
                    Confidence = analysis.Confidence,
                    Reasoning = analysis.Reasoning,
                    StatisticalEdges = analysis.StatisticalEdges,
                    ValueRating = analysis.ValueRating
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing game {GameId}", game.GameId);
                return null;
            }
        }

        private List<SpreadOpportunity> GetBasicAnalysis(List<NBAGameMatchup> games)
        {
            var opportunities = new List<SpreadOpportunity>();

            foreach (var game in games)
            {
                if (!game.Spread.HasValue) continue;

                // Simple heuristic: Look for large spreads that might offer value on the underdog
                var spread = Math.Abs(game.Spread.Value);
                
                if (spread >= 7.0m)
                {
                    opportunities.Add(new SpreadOpportunity
                    {
                        Game = game,
                        RecommendedBet = game.Spread > 0 ? "Take Home" : "Take Away",
                        Confidence = 65 + (int)(spread / 2), // Higher spread = higher confidence in underdog
                        Reasoning = $"Large spread of {spread} points suggests potential value on the underdog. Historical data shows underdogs cover more often when spreads exceed 7 points.",
                        StatisticalEdges = new List<string>
                        {
                            $"Underdog in games with {spread}+ point spreads cover ~52% of the time",
                            "Market may be overvaluing favorite",
                            "Potential garbage time value"
                        },
                        ValueRating = spread >= 10 ? "High Value" : "Medium Value"
                    });
                }
                else if (spread >= 2.5m && spread <= 4.5m)
                {
                    // Close spreads - look for slight favorites
                    opportunities.Add(new SpreadOpportunity
                    {
                        Game = game,
                        RecommendedBet = game.Spread < 0 ? "Take Home" : "Take Away",
                        Confidence = 62,
                        Reasoning = $"Close spread of {spread} points in a competitive matchup. Small favorites often provide value.",
                        StatisticalEdges = new List<string>
                        {
                            "Coin-flip game with slight edge",
                            "Strong team fundamentals",
                            "Home court advantage factor"
                        },
                        ValueRating = "Medium Value"
                    });
                }
            }

            return opportunities.OrderByDescending(o => o.Confidence).Take(5).ToList();
        }

        private class AISpreadAnalysis
        {
            public string RecommendedBet { get; set; } = string.Empty;
            public decimal Confidence { get; set; }
            public string Reasoning { get; set; } = string.Empty;
            public List<string> StatisticalEdges { get; set; } = new();
            public string ValueRating { get; set; } = string.Empty;
        }
    }
}
