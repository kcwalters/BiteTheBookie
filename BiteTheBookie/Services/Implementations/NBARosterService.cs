using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using OpenAI.Chat;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class NBARosterService : INBARosterService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<NBARosterService> _logger;
        private readonly ChatClient? _chatClient;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
        private const string CacheKeyPrefix = "nba_roster_";

        private static readonly Dictionary<string, string> _teamNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ATL", "Atlanta Hawks" },       { "BOS", "Boston Celtics" },
            { "BKN", "Brooklyn Nets" },       { "CHA", "Charlotte Hornets" },
            { "CHI", "Chicago Bulls" },       { "CLE", "Cleveland Cavaliers" },
            { "DAL", "Dallas Mavericks" },    { "DEN", "Denver Nuggets" },
            { "DET", "Detroit Pistons" },     { "GSW", "Golden State Warriors" },
            { "HOU", "Houston Rockets" },     { "IND", "Indiana Pacers" },
            { "LAC", "LA Clippers" },         { "LAL", "Los Angeles Lakers" },
            { "MEM", "Memphis Grizzlies" },   { "MIA", "Miami Heat" },
            { "MIL", "Milwaukee Bucks" },     { "MIN", "Minnesota Timberwolves" },
            { "NOP", "New Orleans Pelicans" },{ "NYK", "New York Knicks" },
            { "OKC", "Oklahoma City Thunder" },{ "ORL", "Orlando Magic" },
            { "PHI", "Philadelphia 76ers" },  { "PHX", "Phoenix Suns" },
            { "POR", "Portland Trail Blazers" },{ "SAC", "Sacramento Kings" },
            { "SAS", "San Antonio Spurs" },   { "TOR", "Toronto Raptors" },
            { "UTA", "Utah Jazz" },           { "WAS", "Washington Wizards" }
        };

        public NBARosterService(
            IMemoryCache cache,
            ILogger<NBARosterService> logger,
            ChatClient? chatClient = null)
        {
            _cache = cache;
            _logger = logger;
            _chatClient = chatClient;
        }

        /// <inheritdoc/>
        public NBATeamRoster GetTeamRoster(string teamCode)
        {
            if (_cache.TryGetValue<NBATeamRoster>($"{CacheKeyPrefix}{teamCode.ToUpper()}", out var cached) && cached != null)
                return cached;

            return EmptyRoster(teamCode);
        }

        /// <inheritdoc/>
        public async Task<NBATeamRoster> GetTeamRosterAsync(string teamCode, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}{teamCode.ToUpper()}";


            if (_cache.TryGetValue<NBATeamRoster>(cacheKey, out var cached) && cached != null)
            {
                _logger.LogDebug("Returning cached roster for {Team}", teamCode);
                return cached;
            }

            if (_chatClient == null)
            {
                _logger.LogWarning("Azure OpenAI ChatClient is not configured — returning empty roster for {Team}.", teamCode);
                return EmptyRoster(teamCode);
            }

            // Attempt to fetch roster from Azure OpenAI
            try
            {
                _logger.LogInformation("Fetching roster for {Team} via Azure OpenAI", teamCode);
                var roster = await FetchRosterFromAzureOpenAIAsync(teamCode, cancellationToken);

                if (roster?.Players != null && roster.Players.Count > 0)
                {
                    _cache.Set(cacheKey, roster, CacheDuration);
                    _logger.LogInformation("Cached roster for {Team} with {PlayerCount} players", teamCode, roster.Players.Count);
                }
                return roster ?? EmptyRoster(teamCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching roster for {Team}. Returning empty roster.", teamCode);
                return EmptyRoster(teamCode);
            }
        }

        private async Task<NBATeamRoster?> FetchRosterFromAzureOpenAIAsync(string teamCode, CancellationToken cancellationToken)
        {
            var teamName = _teamNames.GetValueOrDefault(teamCode.ToUpper(), teamCode);
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var systemPrompt = "You are an AI assistant specializing in providing accurate and up-to-date NBA roster information.";
            var prompt = $@"Please provide the active roster for the NBA team '{teamName}' as of {today}.
Respond ONLY with a valid JSON array in the following format:
[
  {{
    ""name"": ""Full Player Name"",
    ""position"": ""PG"" or ""SG"" or ""SF"" or ""PF"" or ""C"",
    ""isStarter"": true or false,
    ""pointsPerGame"": number,
    ""reboundsPerGame"": number,
    ""assistsPerGame"": number
  }}
]
Additional rules:
1. List all players who are currently active on the roster (12 to 15 players).
2. Use real NBA statistics for the current season.
3. Do NOT include retired, traded, or waived players.
4. Provide realistic positional data (PG, SG, SF, PF, C).";

            var chatMessages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(prompt),
            };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                Temperature = 0.2f
            };

            var response = await _chatClient!.CompleteChatAsync(chatMessages, chatCompletionOptions, cancellationToken);
            var content = response.Value.Content[0].Text.Trim();

            if (content.StartsWith("```json")) content = content["```json".Length..].Trim();
            if (content.StartsWith("```")) content = content.Trim('`').Trim();
            if (content.EndsWith("```")) content = content[..^3].Trim();

            try
            {
                var players = JsonSerializer.Deserialize<List<NBAPlayer>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

                return new NBATeamRoster
                {
                    TeamCode = teamCode.ToUpper(),
                    TeamName = teamName,
                    Players = players ?? new List<NBAPlayer>()
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize roster JSON from OpenAI response for {Team}", teamCode);
                return null;
            }
        }

        private static NBATeamRoster EmptyRoster(string teamCode)
        {
            return new NBATeamRoster
            {
                TeamCode = teamCode.ToUpper(),
                TeamName = _teamNames.GetValueOrDefault(teamCode.ToUpper(), teamCode.ToUpper()),
                Players = new List<NBAPlayer>()
            };
        }
    }
}
