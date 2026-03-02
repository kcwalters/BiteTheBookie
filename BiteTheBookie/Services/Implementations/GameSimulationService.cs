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

Include the following sections in your response using Markdown formatting:

1. **Final Score**: Provide a realistic final score
2. **Game Summary**: Brief overview of how the game played out (2-3 sentences)
3. **Key Performers**: List 3-5 players with their stats (points, rebounds, assists, etc.)
4. **Quarter-by-Quarter Breakdown**: Describe key moments in each quarter with scores at each break
5. **Team Statistics**: Create a comparison table with FG%, 3PT%, rebounds, turnovers, fast-break points
6. **Betting Analysis**: Mention how the game would affect spread, over/under, and moneyline bets

IMPORTANT: 
- Use standard ASCII characters only, NO emojis or special Unicode characters
- Use Markdown tables for statistics
- Make it engaging and informative like a real sports analyst would write it
- Use bold text and headers to organize content clearly";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an expert NBA analyst who creates detailed, realistic game simulations with accurate statistics and engaging narratives. Use only standard ASCII characters in your response."),
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
            return $@"# GAME SIMULATION: {awayTeam} @ {homeTeam}

## Final Score
**{awayTeam}**: 112  
**{homeTeam}**: 108

## Game Summary
In a tightly contested matchup, **{awayTeam}** edges out **{homeTeam}** 112-108 in a thrilling finish. The game was decided in the final minute with clutch free throws and defensive stops. Both teams showed impressive offensive firepower throughout the contest.

## Key Performers

### {awayTeam} Top Players
- **Star Player**: 31 pts, 8 reb, 6 ast - Led the team in scoring with efficient shooting
- **Supporting Player**: 24 pts, 5 reb, 3 stl - Provided crucial defensive stops
- **Role Player**: 19 pts, 11 reb - Dominated the boards

### {homeTeam} Top Players
- **Star Player**: 29 pts, 10 ast, 5 reb - Orchestrated the offense beautifully
- **Supporting Player**: 26 pts, 7 reb - Kept the team in the game with clutch shots
- **Role Player**: 18 pts, 4 ast - Solid contribution off the bench

## Quarter-by-Quarter Breakdown

**1st Quarter**: Close start with both teams trading baskets  
Score: {awayTeam} 28, {homeTeam} 26

**2nd Quarter**: {homeTeam} takes brief lead before halftime  
Halftime Score: {homeTeam} 56, {awayTeam} 54

**3rd Quarter**: {awayTeam} comes out strong, taking control  
End of 3rd: {awayTeam} 83, {homeTeam} 79

**4th Quarter**: Back and forth finish, {awayTeam} seals it late  
Final Score: {awayTeam} 112, {homeTeam} 108

## Team Statistics Comparison

| Statistic | {awayTeam} | {homeTeam} |
|-----------|------------|------------|
| FG% | 48% | 46% |
| 3-Pointers | 13/32 (40.6%) | 11/35 (31.4%) |
| Rebounds | 42 | 45 |
| Turnovers | 12 | 14 |
| Fast-break Points | 16 | 11 |

## Betting Analysis

**Spread**: {awayTeam} wins by 4 points - check your spread line  
**Over/Under**: Total of 220 points  
**Moneyline**: {awayTeam} wins outright

---

*This is a simulated game for entertainment purposes. Results may vary from actual games.*";
        }
    }
}
