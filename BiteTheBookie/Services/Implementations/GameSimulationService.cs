using BiteTheBookie.Services.Interfaces;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace BiteTheBookie.Services.Implementations
{
    public class GameSimulationService : IGameSimulationService
    {
        private readonly ChatClient _chatClient;
        private readonly ILogger<GameSimulationService> _logger;

        public GameSimulationService(IConfiguration configuration, ILogger<GameSimulationService> logger)
        {
            _logger = logger;

            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var apiKey = configuration["AzureOpenAI:ApiKey"];
            var deploymentName = configuration["AzureOpenAI:DeploymentName"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("Azure OpenAI configuration is missing. Service will return mock data.");
                _chatClient = null!;
                return;
            }

            var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            _chatClient = azureClient.GetChatClient(deploymentName);
        }

        public async Task<string> GenerateGameSimulationAsync(string homeTeam, string awayTeam, string league, CancellationToken cancellationToken = default)
        {
            // If no API configuration, return mock data
            if (_chatClient == null)
            {
                _logger.LogInformation("Using mock simulation data for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
                return GetMockSimulation(homeTeam, awayTeam);
            }

            try
            {
                var prompt = $@"Generate a detailed sports game simulation for an NBA game between {awayTeam} (away) and {homeTeam} (home). 

Include the following sections in your response:

1. **Final Score**: Provide a realistic final score
2. **Game Summary**: Brief overview of how the game played out (2-3 sentences)
3. **Key Performers**: List 3-5 players with their stats (points, rebounds, assists, etc.)
4. **Quarter-by-Quarter Breakdown**: Describe key moments in each quarter with scores at each break
5. **Team Statistics**: Compare both teams (FG%, 3PT%, rebounds, turnovers, fast-break points)
6. **Betting Insights**: Mention how the game would affect spread, over/under, and moneyline bets

Format the response in a clear, readable way with emojis for visual appeal. Make it engaging and informative like a real sports analyst would write it.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an expert NBA analyst who creates detailed, realistic game simulations with accurate statistics and engaging narratives."),
                    new UserChatMessage(prompt)
                };

                var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
                
                return response.Value.Content[0].Text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating game simulation for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
                return GetMockSimulation(homeTeam, awayTeam);
            }
        }

        private static string GetMockSimulation(string homeTeam, string awayTeam)
        {
            return $@"# ?? Game Simulation: {awayTeam} @ {homeTeam}

## Final Score
**{awayTeam}**: 112
**{homeTeam}**: 108

## ?? Game Summary
In a tightly contested matchup, {awayTeam} edges out {homeTeam} 112-108 in a thrilling finish. The game was decided in the final minute with clutch free throws and defensive stops. Both teams showed impressive offensive firepower throughout the contest.

## ? Key Performers

### {awayTeam}
- **Star Player**: 31 pts, 8 reb, 6 ast
- **Supporting Player**: 24 pts, 5 reb, 3 stl
- **Role Player**: 19 pts, 11 reb

### {homeTeam}
- **Star Player**: 29 pts, 10 ast, 5 reb
- **Supporting Player**: 26 pts, 7 reb
- **Role Player**: 18 pts, 4 ast

## ?? Quarter Breakdown

**Q1**: Close start with both teams trading baskets - {awayTeam} 28, {homeTeam} 26
**Q2**: {homeTeam} takes brief lead before halftime - Halftime: {homeTeam} 56, {awayTeam} 54
**Q3**: {awayTeam} comes out strong, taking control - End Q3: {awayTeam} 83, {homeTeam} 79
**Q4**: Back and forth finish, {awayTeam} seals it late - Final: {awayTeam} 112, {homeTeam} 108

## ?? Team Statistics
- **FG%**: {awayTeam} 48% | {homeTeam} 46%
- **3PT**: {awayTeam} 13/32 | {homeTeam} 11/35
- **Rebounds**: {awayTeam} 42 | {homeTeam} 45
- **Turnovers**: {awayTeam} 12 | {homeTeam} 14
- **Fast-break**: {awayTeam} 16 | {homeTeam} 11

## ?? Betting Impact
Based on this simulation:
- **Spread**: Check current spread to see if {awayTeam} covers
- **Over/Under**: Total of 220 points - compare to betting line
- **Moneyline**: {awayTeam} wins outright

*Note: This is a simulated game for entertainment purposes.*";
        }
    }
}
