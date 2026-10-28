using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using OpenAI.Chat;

namespace BiteTheBookie.Services.Implementations
{

    public class NFLScoresService : INFLScoresService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NFLScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly string _scoreboardUrl;
        private readonly ChatClient? _chatClient;

        private const string DefaultScoreboardUrl =
            "https://site.web.api.espn.com/apis/site/v2/sports/football/nfl/scoreboard";
        private const string CacheKey = "nfl:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public NFLScoresService(HttpClient httpClient, ILogger<NFLScoresService> logger, IMemoryCache cache, IOptions<SportsTickerOptions> options, ChatClient? chatClient = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _chatClient = chatClient;
            var cfg = options.Value;
            _scoreboardUrl = string.IsNullOrWhiteSpace(cfg.NflApiBaseUrl) ? DefaultScoreboardUrl : cfg.NflApiBaseUrl.TrimEnd('/');
        }

        public async Task<IReadOnlyList<NFLTickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NFLTickerView>? cached) && cached is not null)
            {
                _logger.LogDebug("Returning NFL scores from cache.");
                return cached;
            }

            try
            {
                // Fetch today's games, falling forward to the next scheduled day when the
                // league is off today (off-day or out of season) so the ticker stays populated.
                var events = await EspnScoreboardFetcher.GetEarliestDayEventsAsync(
                    _httpClient, _scoreboardUrl, cancellationToken);

                if (events.Count == 0)
                {
                    _logger.LogInformation("ESPN NFL scoreboard: no games today or upcoming. Falling back to OpenAI.");
                    var aiGames = await GetGamesFromOpenAiAsync(cancellationToken);
                    var fallback = (IReadOnlyList<NFLTickerView>)aiGames;
                    _cache.Set(CacheKey, fallback, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                    return fallback;
                }

                var games = new List<NFLTickerView>();
                foreach (var ev in events)
                {
                    var game = MapEventToTickerGame(ev);
                    if (game is not null)
                    {
                        games.Add(game.Value);
                    }
                }

                var result = (IReadOnlyList<NFLTickerView>)games;
                _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NFL scoreboard from ESPN. Falling back to OpenAI.");
                var aiGames = await GetGamesFromOpenAiAsync(cancellationToken);
                var fallback = (IReadOnlyList<NFLTickerView>)aiGames;
                _cache.Set(CacheKey, fallback, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                return fallback;
            }
        }

        /// <summary>
        /// Last-resort fallback that asks Azure OpenAI for the current or upcoming NFL slate
        /// when ESPN is unavailable or returns no games. Returns an empty list if the
        /// ChatClient is not configured or the response cannot be parsed.
        /// </summary>
        private async Task<List<NFLTickerView>> GetGamesFromOpenAiAsync(CancellationToken cancellationToken)
        {
            if (_chatClient == null)
            {
                _logger.LogWarning("NFLScoresService: Azure OpenAI ChatClient is not configured. Ticker will be empty.");
                return new List<NFLTickerView>();
            }

            try
            {
                var systemPrompt =
                    "You are a sports data assistant. Return the current or next scheduled slate of NFL games. " +
                    "Respond ONLY with a compact JSON array and nothing else. Each element must use exactly these keys: " +
                    "\"awayTeam\" (team abbreviation, e.g. \"KC\"), \"homeTeam\" (team abbreviation), " +
                    "\"awayScore\" (integer or null), \"homeScore\" (integer or null), " +
                    "\"statusText\" (e.g. \"Sun 1:00 PM\", \"Final\", \"Q3 5:21\"). " +
                    "Do not wrap the JSON in code fences. Return between 1 and 16 games.";

                var userPrompt =
                    $"Today's date (UTC): {DateTime.UtcNow:yyyy-MM-dd}. " +
                    "List the upcoming NFL games for the current or next game day with team abbreviations and scheduled kickoff times.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                var options = new ChatCompletionOptions { Temperature = 0.2f };
                var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
                var raw = response.Value.Content.Count > 0 ? response.Value.Content[0].Text : null;

                if (string.IsNullOrWhiteSpace(raw))
                {
                    _logger.LogWarning("NFLScoresService: empty OpenAI fallback response.");
                    return new List<NFLTickerView>();
                }

                return ParseOpenAiGames(raw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NFLScoresService: OpenAI fallback failed.");
                return new List<NFLTickerView>();
            }
        }

        private List<NFLTickerView> ParseOpenAiGames(string raw)
        {
            var games = new List<NFLTickerView>();

            // Strip accidental code fences and isolate the JSON array.
            var text = raw.Trim();
            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');
            if (start < 0 || end <= start)
            {
                _logger.LogWarning("NFLScoresService: OpenAI fallback did not contain a JSON array: {Raw}", raw);
                return games;
            }

            var json = text.Substring(start, end - start + 1);

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return games;
                }

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string away = GetStringProp(el, "awayTeam");
                    string home = GetStringProp(el, "homeTeam");
                    if (string.IsNullOrWhiteSpace(away) || string.IsNullOrWhiteSpace(home))
                    {
                        continue;
                    }

                    int? awayScore = GetIntProp(el, "awayScore");
                    int? homeScore = GetIntProp(el, "homeScore");
                    string status = GetStringProp(el, "statusText");

                    bool isFinal = status.Contains("final", StringComparison.OrdinalIgnoreCase);

                    games.Add(new NFLTickerView(
                        AwayTeam: away,
                        HomeTeam: home,
                        AwayScore: awayScore,
                        HomeScore: homeScore,
                        AwayLogo: $"/img/nfl/{away.ToLowerInvariant()}.svg",
                        HomeLogo: $"/img/nfl/{home.ToLowerInvariant()}.svg",
                        StatusText: string.IsNullOrWhiteSpace(status) ? "Scheduled" : status,
                        IsLive: false,
                        IsFinal: isFinal,
                        EventId: null));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NFLScoresService: failed to parse OpenAI fallback JSON: {Json}", json);
            }

            return games;
        }

        private static string GetStringProp(JsonElement el, string name)
            => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? string.Empty
                : string.Empty;

        private static int? GetIntProp(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out var p))
            {
                if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n))
                {
                    return n;
                }
                if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
            return null;
        }

        public async Task<IReadOnlyList<NFLTickerView>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var events = await EspnScoreboardFetcher.GetEventsForDateAsync(
                    _httpClient, _scoreboardUrl, date, cancellationToken);

                var games = events.Select(MapEventToTickerGame)
                                  .Where(game => game is not null)
                                  .Select(game => game.Value)
                                  .ToList();

                return games;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NFL scoreboard for date {Date}", date);
                return Array.Empty<NFLTickerView>();
            }
        }

        private static NFLTickerView? MapEventToTickerGame(JsonElement ev)
        {
            if (!ev.TryGetProperty("competitions", out var compsElement) ||
                compsElement.ValueKind != JsonValueKind.Array ||
                compsElement.GetArrayLength() == 0)
            {
                return null;
            }

            var comp = compsElement[0];

            string state = "";
            string shortDetail = "";
            if (comp.TryGetProperty("status", out var statusElement) &&
                statusElement.TryGetProperty("type", out var typeElement))
            {
                state = typeElement.GetProperty("state").GetString() ?? "";
                shortDetail = typeElement.GetProperty("shortDetail").GetString() ?? "";
            }

            string awayTeam = "";
            string homeTeam = "";
            int? awayScore = null;
            int? homeScore = null;
            string awayLogo = "";
            string homeLogo = "";

            if (comp.TryGetProperty("competitors", out var competitorsElement) &&
                competitorsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var teamElement in competitorsElement.EnumerateArray())
                {
                    var homeAway = teamElement.GetProperty("homeAway").GetString();
                    var teamNode = teamElement.GetProperty("team");

                    var teamAbbr = teamNode.GetProperty("abbreviation").GetString() ?? "";
                    var logoUrl = GetLogoUrl(teamNode, teamAbbr);

                    int? score = null;
                    if (teamElement.TryGetProperty("score", out var scoreElement) &&
                        scoreElement.ValueKind == JsonValueKind.String &&
                        int.TryParse(scoreElement.GetString(), out var parsedScore))
                    {
                        score = parsedScore;
                    }

                    if (homeAway == "away")
                    {
                        awayTeam = teamAbbr;
                        awayScore = score;
                        awayLogo = logoUrl;
                    }
                    else
                    {
                        homeTeam = teamAbbr;
                        homeScore = score;
                        homeLogo = logoUrl;
                    }
                }
            }

            string? eventId = ev.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;

            bool isLive = state == "in";
            bool isFinal = state == "post";

            var statusText = !string.IsNullOrWhiteSpace(shortDetail)
                ? shortDetail
                : state switch
                {
                    "pre" => "Scheduled",
                    "in" => "Live",
                    "post" => "Final",
                    _ => state
                };

            return new NFLTickerView(
                awayTeam,
                homeTeam,
                awayScore,
                homeScore,
                awayLogo,
                homeLogo,
                statusText,
                isLive,
                isFinal,
                eventId
            );
        }

        private static string GetLogoUrl(JsonElement teamNode, string teamAbbr)
        {
            if (teamNode.TryGetProperty("logos", out var logosElement) &&
                logosElement.ValueKind == JsonValueKind.Array &&
                logosElement.GetArrayLength() > 0)
            {
                var href = logosElement[0].GetProperty("href").GetString();
                if (!string.IsNullOrWhiteSpace(href))
                {
                    return href!;
                }
            }

            if (teamNode.TryGetProperty("logo", out var logoElement) &&
                logoElement.ValueKind == JsonValueKind.String)
            {
                var href = logoElement.GetString();
                if (!string.IsNullOrWhiteSpace(href))
                {
                    return href!;
                }
            }

            return $"/img/nfl/{teamAbbr.ToLowerInvariant()}.svg";
        }
    }
}