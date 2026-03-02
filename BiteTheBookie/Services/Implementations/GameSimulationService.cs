using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.Models;
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

        public async Task<string> GenerateGameSimulationAsync(
            string homeTeam, 
            string awayTeam, 
            string league, 
            NBATeamRoster? homeRoster = null, 
            NBATeamRoster? awayRoster = null, 
            CancellationToken cancellationToken = default)
        {
            // Always generate a fresh simulation - no caching
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var simulationId = Guid.NewGuid().ToString("N").Substring(0, 8);
            
            _logger.LogInformation("Generating NEW simulation #{SimulationId} for {HomeTeam} vs {AwayTeam} at {Timestamp}", 
                simulationId, homeTeam, awayTeam, timestamp);
            
            // If no API configuration, return mock data
            if (_chatClient == null)
            {
                _logger.LogInformation("Using mock simulation data for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
                return GetMockSimulation(homeTeam, awayTeam, homeRoster, awayRoster);
            }

            try
            {
                _logger.LogInformation("Generating AI simulation for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
                
                // Build player roster strings
                var awayStarters = awayRoster?.Players.Where(p => p.IsStarter).Select(p => $"{p.Name} ({p.Position})").ToList() ?? new List<string>();
                var homeStarters = homeRoster?.Players.Where(p => p.IsStarter).Select(p => $"{p.Name} ({p.Position})").ToList() ?? new List<string>();
                var awayBench = awayRoster?.Players.Where(p => !p.IsStarter).Select(p => p.Name).ToList() ?? new List<string>();
                var homeBench = homeRoster?.Players.Where(p => !p.IsStarter).Select(p => p.Name).ToList() ?? new List<string>();

                var rosterInfo = $@"
**{awayTeam} Roster:**
Starting 5: {string.Join(", ", awayStarters)}
Key Bench: {string.Join(", ", awayBench)}

**{homeTeam} Roster:**
Starting 5: {string.Join(", ", homeStarters)}
Key Bench: {string.Join(", ", homeBench)}
";

                var prompt = $@"Generate a FRESH, UNIQUE sports game simulation for an NBA game between {awayTeam} (away) and {homeTeam} (home). 

SIMULATION ID: {simulationId}
GENERATED AT: {timestamp}

{rosterInfo}

CRITICAL INSTRUCTIONS: 
- This is simulation #{simulationId} - make it COMPLETELY DIFFERENT from any previous simulations
- Use ONLY the players listed above in your simulation
- Base the key performers and statistics on these ACTUAL PLAYERS
- Make this simulation UNIQUE and SPECIFIC to {awayTeam} vs {homeTeam}
- Consider the actual strengths and playing styles of these teams
- DO NOT generate a generic simulation - make it about THIS SPECIFIC matchup
- VARY the final score each time - don't always use the same score
- CHANGE which players are the top performers - simulate different game scenarios
- CREATE DIFFERENT game narratives - sometimes close games, sometimes blowouts, sometimes comeback wins

Include the following sections in your response using Markdown formatting:

1. **Final Score**: Provide a realistic but VARIED final score (don't repeat scores from other simulations)
2. **Game Summary**: Brief overview specific to how {awayTeam} vs {homeTeam} played out (2-3 sentences) mentioning specific players from the rosters above
3. **Key Performers**: List 3-5 ACTUAL players from the rosters above with realistic stats. VARY which players have big games each simulation.
4. **Quarter-by-Quarter Breakdown**: Describe key moments in each quarter with scores at each break, mentioning specific players by name and specific plays
5. **Team Statistics**: Create a comparison table with FG%, 3PT%, rebounds, turnovers, fast-break points
6. **Betting Analysis**: Mention how the game would affect spread, over/under, and moneyline bets

CRITICAL REQUIREMENTS:
- Use ONLY players from the rosters provided above
- Use realistic statistics appropriate for each player's actual skill level and position
- Make the narrative engaging and SPECIFIC to {awayTeam} vs {homeTeam} matchup
- Consider actual team dynamics (e.g., if {awayTeam} has a dominant center, mention how they attacked the paint)
- Use standard ASCII characters only, NO emojis or special Unicode characters
- Use Markdown tables for statistics
- Be specific about which players made key plays
- Make each simulation DIFFERENT - vary the scores, key plays, and narratives based on the teams involved";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage($"You are an expert NBA analyst who creates detailed, realistic game simulations. Each simulation must be UNIQUE with different scores, different star performers, and different narratives. Generate simulation #{simulationId} with fresh content - never repeat previous simulations. Use actual player names from rosters and vary which players have big games. Use only standard ASCII characters."),
                    new UserChatMessage(prompt)
                };

                // Use higher temperature for more varied, creative responses
                var chatOptions = new ChatCompletionOptions
                {
                    Temperature = 0.9f // Higher temperature for more randomness and creativity
                };

                var response = await _chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
                
                _logger.LogInformation("Successfully generated UNIQUE AI simulation #{SimulationId} for {HomeTeam} vs {AwayTeam}", 
                    simulationId, homeTeam, awayTeam);
                
                return response.Value.Content[0].Text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating game simulation for {HomeTeam} vs {AwayTeam}, falling back to mock", homeTeam, awayTeam);
                return GetMockSimulation(homeTeam, awayTeam, homeRoster, awayRoster);
            }
        }

        private static string GetMockSimulation(string homeTeam, string awayTeam, NBATeamRoster? homeRoster, NBATeamRoster? awayRoster)
        {
            // Get actual player names if rosters are available
            var awayPlayers = awayRoster?.Players.Where(p => p.IsStarter).Take(3).ToList() ?? new List<NBAPlayer>();
            var homePlayers = homeRoster?.Players.Where(p => p.IsStarter).Take(3).ToList() ?? new List<NBAPlayer>();

            string awayPlayer1 = awayPlayers.Count > 0 ? awayPlayers[0].Name : "Star Player";
            string awayPlayer2 = awayPlayers.Count > 1 ? awayPlayers[1].Name : "Supporting Player";
            string awayPlayer3 = awayPlayers.Count > 2 ? awayPlayers[2].Name : "Role Player";

            string homePlayer1 = homePlayers.Count > 0 ? homePlayers[0].Name : "Star Player";
            string homePlayer2 = homePlayers.Count > 1 ? homePlayers[1].Name : "Supporting Player";
            string homePlayer3 = homePlayers.Count > 2 ? homePlayers[2].Name : "Role Player";

            // Generate UNIQUE scores based on timestamp AND team names for true randomness
            var seed = (homeTeam + awayTeam + DateTime.UtcNow.Ticks).GetHashCode();
            var random = new Random(seed);
            
            int awayScore = random.Next(95, 125);
            int homeScore = random.Next(95, 125);
            
            // Ensure scores are close for excitement
            if (Math.Abs(awayScore - homeScore) > 15)
            {
                if (awayScore > homeScore)
                    awayScore = homeScore + random.Next(1, 12);
                else
                    homeScore = awayScore + random.Next(1, 12);
            }

            bool awayWins = awayScore > homeScore;
            string winner = awayWins ? awayTeam : homeTeam;
            string loser = awayWins ? homeTeam : awayTeam;
            int margin = Math.Abs(awayScore - homeScore);

            // Generate varied player stats
            int player1Points = random.Next(25, 38);
            int player1Rebounds = random.Next(5, 13);
            int player1Assists = random.Next(4, 11);

            int player2Points = random.Next(18, 28);
            int player2Rebounds = random.Next(4, 9);

            int player3Points = random.Next(12, 22);
            int player3Rebounds = random.Next(8, 14);

            int homePlayer1Points = random.Next(23, 35);
            int homePlayer1Assists = random.Next(6, 12);
            int homePlayer2Points = random.Next(20, 30);

            // Quarter scores
            int q1Away = random.Next(22, 32);
            int q1Home = random.Next(22, 32);
            int halfAway = random.Next(48, 62);
            int halfHome = random.Next(48, 62);
            int q3Away = random.Next(72, 92);
            int q3Home = random.Next(72, 92);

            return $@"# GAME SIMULATION: {awayTeam} @ {homeTeam}

## Final Score
**{awayTeam}**: {awayScore}  
**{homeTeam}**: {homeScore}

## Game Summary
In an exciting {(margin < 5 ? "nail-biter" : "hard-fought battle")}, **{winner}** {(margin < 5 ? "narrowly defeats" : "edges")} **{loser}** {awayScore}-{homeScore}. {awayPlayer1} was the driving force for {awayTeam} with {player1Points} points, while {homePlayer1} led {homeTeam} with an impressive {homePlayer1Points}-point, {homePlayer1Assists}-assist performance. The game featured multiple lead changes and came down to execution in the final minutes.

## Key Performers

### {awayTeam} Top Players
- **{awayPlayer1}**: {player1Points} pts, {player1Rebounds} reb, {player1Assists} ast - Dominated on both ends of the floor with efficient scoring and clutch plays
- **{awayPlayer2}**: {player2Points} pts, {player2Rebounds} reb, 3 stl - Provided crucial defensive pressure and timely buckets
- **{awayPlayer3}**: {player3Points} pts, {player3Rebounds} reb - Controlled the paint and won the battle on the boards

### {homeTeam} Top Players
- **{homePlayer1}**: {homePlayer1Points} pts, {homePlayer1Assists} ast, 5 reb - Orchestrated the offense brilliantly and created opportunities for teammates
- **{homePlayer2}**: {homePlayer2Points} pts, 7 reb - Kept the team competitive with aggressive attacks and clutch shooting
- **{homePlayer3}**: {random.Next(15, 22)} pts, 4 ast - Provided steady contribution and solid two-way play

## Quarter-by-Quarter Breakdown

**1st Quarter**: Both teams trade baskets early. {awayPlayer1} establishes himself while {homePlayer1} answers back.  
Score: {awayTeam} {q1Away}, {homeTeam} {q1Home}

**2nd Quarter**: {homePlayer2} heats up, but {awayPlayer2} responds with tough defense and transition buckets.  
Halftime Score: {awayTeam} {halfAway}, {homeTeam} {halfHome}

**3rd Quarter**: {awayPlayer3} dominates inside, giving {awayTeam} momentum. {homeTeam} battles to stay close.  
End of 3rd: {awayTeam} {q3Away}, {homeTeam} {q3Home}

**4th Quarter**: {(awayWins ? $"{awayPlayer1} takes over" : $"{homePlayer1} leads the comeback")} in crunch time. {winner} makes key plays down the stretch to secure the victory.  
Final Score: {awayTeam} {awayScore}, {homeTeam} {homeScore}

## Team Statistics Comparison

| Statistic | {awayTeam} | {homeTeam} |
|-----------|------------|------------|
| FG% | {random.Next(44, 52)}% | {random.Next(43, 51)}% |
| 3-Pointers | {random.Next(10, 16)}/{random.Next(28, 38)} ({random.Next(32, 45)}%) | {random.Next(9, 15)}/{random.Next(27, 40)} ({random.Next(30, 43)}%) |
| Rebounds | {random.Next(38, 48)} | {random.Next(40, 50)} |
| Turnovers | {random.Next(10, 16)} | {random.Next(11, 17)} |
| Fast-break Points | {random.Next(12, 22)} | {random.Next(10, 20)} |

## Betting Analysis

**Spread**: {winner} wins by {margin} points - {(margin > 5 ? "likely covers the spread" : "close to the spread line")}  
**Over/Under**: Total of {awayScore + homeScore} points  
**Moneyline**: {winner} wins outright

---

*This is a simulated game for entertainment purposes. Results and statistics are generated for demonstration.*";
        }
    }
}
