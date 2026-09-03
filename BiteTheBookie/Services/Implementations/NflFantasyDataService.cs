using System.Text.Json;
using BiteTheBookie.Models.Fantasy;
using BiteTheBookie.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Provides Daily Fantasy Football data sourced entirely from the public ESPN API:
    /// the slate (scoreboard), the player pool (team rosters) with derived salaries, and
    /// finalized fantasy points (box scores). No mock data is used.
    /// </summary>
    public class NflFantasyDataService : INflFantasyDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NflFantasyDataService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IFantasyScoringService _scoring;

        private const string ScoreboardPath = "apis/site/v2/sports/football/nfl/scoreboard";
        private const string RosterPathFormat = "apis/site/v2/sports/football/nfl/teams/{0}/roster";
        private const string SummaryPathFormat = "apis/site/v2/sports/football/nfl/summary?event={0}";

        private static readonly TimeSpan SlateTtl = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan PoolTtl = TimeSpan.FromMinutes(15);

        // Fantasy-eligible offensive positions we surface individually.
        private static readonly HashSet<string> OffensePositions =
            new(StringComparer.OrdinalIgnoreCase) { "QB", "RB", "WR", "TE" };

        public NflFantasyDataService(
            HttpClient httpClient,
            ILogger<NflFantasyDataService> logger,
            IMemoryCache cache,
            IFantasyScoringService scoring)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _scoring = scoring;
            _httpClient.BaseAddress ??= new Uri("https://site.web.api.espn.com/");
        }

        public async Task<IReadOnlyList<FantasySlateGame>> GetSlateGamesAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var dateKey = date.ToString("yyyyMMdd");
            var cacheKey = $"fantasy:slate:{dateKey}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<FantasySlateGame>? cached) && cached is not null)
                return cached;

            var games = new List<FantasySlateGame>();
            try
            {
                using var response = await _httpClient.GetAsync($"{ScoreboardPath}?dates={dateKey}", cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (doc.RootElement.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ev in events.EnumerateArray())
                    {
                        var game = MapSlateGame(ev);
                        if (game is not null)
                            games.Add(game);
                    }
                }

                games = games.OrderBy(g => g.GameTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load NFL fantasy slate for {Date}", dateKey);
            }

            _cache.Set(cacheKey, (IReadOnlyList<FantasySlateGame>)games,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = SlateTtl });
            return games;
        }

        public async Task<IReadOnlyList<FantasyPlayer>> BuildPlayerPoolAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var dateKey = date.ToString("yyyyMMdd");
            var cacheKey = $"fantasy:pool:{dateKey}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<FantasyPlayer>? cached) && cached is not null)
                return cached;

            var pool = new List<FantasyPlayer>();
            var slate = await GetSlateGamesAsync(date, cancellationToken);

            foreach (var game in slate)
            {
                // Add offensive players from both teams' real rosters.
                var awayPlayers = await BuildTeamPlayersAsync(game, isHome: false, cancellationToken);
                var homePlayers = await BuildTeamPlayersAsync(game, isHome: true, cancellationToken);
                pool.AddRange(awayPlayers);
                pool.AddRange(homePlayers);

                // Add each team as a DST option.
                pool.Add(BuildDefense(game, isHome: false));
                pool.Add(BuildDefense(game, isHome: true));
            }

            AssignSalaries(pool);

            _cache.Set(cacheKey, (IReadOnlyList<FantasyPlayer>)pool,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = PoolTtl });
            return pool;
        }

        public async Task<IReadOnlyDictionary<string, decimal>> GetActualFantasyPointsAsync(FantasyContest contest, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (contest?.Players is null)
                return result;

            // Group players by their real game id so we fetch each box score once.
            var gameIds = contest.Players
                .Where(p => !string.IsNullOrWhiteSpace(p.GameId))
                .Select(p => p.GameId)
                .Distinct()
                .ToList();

            foreach (var gameId in gameIds)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(string.Format(SummaryPathFormat, gameId), cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    ParseBoxScore(doc.RootElement, result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch box score for game {GameId}", gameId);
                }
            }

            return result;
        }

        // ---------------------------------------------------------------------
        // Slate parsing
        // ---------------------------------------------------------------------
        private static FantasySlateGame? MapSlateGame(JsonElement ev)
        {
            if (!ev.TryGetProperty("competitions", out var comps) ||
                comps.ValueKind != JsonValueKind.Array || comps.GetArrayLength() == 0)
                return null;

            var comp = comps[0];
            var gameId = ev.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;

            DateTime gameTime = ev.TryGetProperty("date", out var dateEl) && dateEl.TryGetDateTime(out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow;

            string state = "pre";
            if (comp.TryGetProperty("status", out var statusEl) && statusEl.TryGetProperty("type", out var typeEl) &&
                typeEl.TryGetProperty("state", out var stateEl))
            {
                state = stateEl.GetString() ?? "pre";
            }

            var status = state switch { "in" => "Live", "post" => "Final", _ => "Scheduled" };

            string awayCode = "", awayName = "", homeCode = "", homeName = "";
            if (comp.TryGetProperty("competitors", out var competitors) && competitors.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in competitors.EnumerateArray())
                {
                    var homeAway = c.TryGetProperty("homeAway", out var haEl) ? haEl.GetString() : null;
                    var team = c.TryGetProperty("team", out var teamEl) ? teamEl : default;
                    var abbr = team.ValueKind == JsonValueKind.Object && team.TryGetProperty("abbreviation", out var abEl)
                        ? abEl.GetString() ?? "" : "";
                    var displayName = team.ValueKind == JsonValueKind.Object && team.TryGetProperty("displayName", out var dnEl)
                        ? dnEl.GetString() ?? abbr : abbr;

                    if (homeAway == "away") { awayCode = abbr; awayName = displayName; }
                    else { homeCode = abbr; homeName = displayName; }
                }
            }

            if (string.IsNullOrEmpty(homeCode) || string.IsNullOrEmpty(awayCode) || string.IsNullOrEmpty(gameId))
                return null;

            return new FantasySlateGame
            {
                GameId = gameId,
                AwayTeamCode = awayCode,
                AwayTeamName = awayName,
                HomeTeamCode = homeCode,
                HomeTeamName = homeName,
                GameTime = gameTime,
                Status = status
            };
        }

        // ---------------------------------------------------------------------
        // Player pool
        // ---------------------------------------------------------------------
        private async Task<List<FantasyPlayer>> BuildTeamPlayersAsync(FantasySlateGame game, bool isHome, CancellationToken cancellationToken)
        {
            var teamCode = isHome ? game.HomeTeamCode : game.AwayTeamCode;
            var teamName = isHome ? game.HomeTeamName : game.AwayTeamName;
            var opponent = isHome ? game.AwayTeamCode : game.HomeTeamCode;
            var players = new List<FantasyPlayer>();

            try
            {
                using var response = await _httpClient.GetAsync(
                    string.Format(RosterPathFormat, teamCode.ToLowerInvariant()), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ESPN roster returned {Status} for {Team}", response.StatusCode, teamCode);
                    return players;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (root.TryGetProperty("athletes", out var athletesEl) && athletesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var group in athletesEl.EnumerateArray())
                    {
                        if (group.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var athlete in items.EnumerateArray())
                            {
                                var player = ParseRosterAthlete(athlete, game, teamCode, teamName, opponent);
                                if (player is not null)
                                    players.Add(player);
                            }
                        }
                        else
                        {
                            var player = ParseRosterAthlete(group, game, teamCode, teamName, opponent);
                            if (player is not null)
                                players.Add(player);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build player list for {Team}", teamCode);
            }

            return players;
        }

        private static FantasyPlayer? ParseRosterAthlete(JsonElement athlete, FantasySlateGame game, string teamCode, string teamName, string opponent)
        {
            string position = "";
            if (athlete.TryGetProperty("position", out var posEl) && posEl.ValueKind == JsonValueKind.Object &&
                posEl.TryGetProperty("abbreviation", out var posAbbr))
            {
                position = posAbbr.GetString() ?? "";
            }

            if (!OffensePositions.Contains(position))
                return null;

            var id = athlete.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            var name = athlete.TryGetProperty("fullName", out var nameEl) ? nameEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                return null;

            string? headshot = null;
            if (athlete.TryGetProperty("headshot", out var hsEl) && hsEl.ValueKind == JsonValueKind.Object &&
                hsEl.TryGetProperty("href", out var hsHref))
            {
                headshot = hsHref.GetString();
            }

            return new FantasyPlayer
            {
                PlayerId = id,
                PlayerName = name,
                Position = position.ToUpperInvariant(),
                TeamCode = teamCode.ToUpperInvariant(),
                TeamName = teamName,
                OpponentCode = opponent.ToUpperInvariant(),
                GameId = game.GameId,
                GameTime = game.GameTime,
                ImageUrl = headshot
            };
        }

        private static FantasyPlayer BuildDefense(FantasySlateGame game, bool isHome)
        {
            var teamCode = isHome ? game.HomeTeamCode : game.AwayTeamCode;
            var teamName = isHome ? game.HomeTeamName : game.AwayTeamName;
            var opponent = isHome ? game.AwayTeamCode : game.HomeTeamCode;

            return new FantasyPlayer
            {
                PlayerId = $"DST-{teamCode}",
                PlayerName = $"{teamName} D/ST",
                Position = "DST",
                TeamCode = teamCode.ToUpperInvariant(),
                TeamName = teamName,
                OpponentCode = opponent.ToUpperInvariant(),
                GameId = game.GameId,
                GameTime = game.GameTime,
                ImageUrl = $"https://a.espncdn.com/i/teamlogos/nfl/500/{teamCode.ToLowerInvariant()}.png"
            };
        }

        /// <summary>
        /// Derives DFS salaries deterministically from each player's position tier and roster
        /// ordering (ESPN provides no salary field). Salaries land within the configured band and
        /// snap to the rounding step.
        /// </summary>
        private static void AssignSalaries(List<FantasyPlayer> pool)
        {
            // Baseline positional salary anchors (mid-band), scaled within a position group.
            var positionAnchor = new Dictionary<string, (int High, int Low)>(StringComparer.OrdinalIgnoreCase)
            {
                ["QB"] = (8200, 5200),
                ["RB"] = (8800, 4000),
                ["WR"] = (8600, 3800),
                ["TE"] = (6800, 3000),
                ["DST"] = (4200, 2400),
            };

            foreach (var group in pool.GroupBy(p => p.Position))
            {
                if (!positionAnchor.TryGetValue(group.Key, out var band))
                    band = (7000, 3500);

                // Order within a position group is preserved from the roster (depth chart order),
                // which correlates with real usage; top of the list gets the higher salary.
                var members = group.ToList();
                var count = members.Count;
                for (int i = 0; i < count; i++)
                {
                    double t = count <= 1 ? 0 : (double)i / (count - 1);
                    int salary = (int)Math.Round(band.High - t * (band.High - band.Low));
                    salary = (salary / 100) * 100; // snap to $100
                    members[i].Salary = salary;
                }
            }
        }

        // ---------------------------------------------------------------------
        // Box-score scoring (real completed-game stats)
        // ---------------------------------------------------------------------
        private void ParseBoxScore(JsonElement summary, Dictionary<string, decimal> result)
        {
            if (!summary.TryGetProperty("boxscore", out var boxscore))
                return;

            // Offensive players: boxscore.players[].statistics[] with athletes[].
            if (boxscore.TryGetProperty("players", out var teamsPlayers) && teamsPlayers.ValueKind == JsonValueKind.Array)
            {
                foreach (var teamBlock in teamsPlayers.EnumerateArray())
                {
                    if (!teamBlock.TryGetProperty("statistics", out var statCategories) || statCategories.ValueKind != JsonValueKind.Array)
                        continue;

                    // Accumulate per-athlete stat lines across categories (passing/rushing/receiving/fumbles).
                    var lines = new Dictionary<string, FantasyStatLine>(StringComparer.OrdinalIgnoreCase);

                    foreach (var category in statCategories.EnumerateArray())
                    {
                        var catName = category.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                        var labels = category.TryGetProperty("labels", out var lbls) && lbls.ValueKind == JsonValueKind.Array
                            ? lbls.EnumerateArray().Select(l => l.GetString() ?? "").ToArray()
                            : Array.Empty<string>();

                        if (!category.TryGetProperty("athletes", out var athletes) || athletes.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var a in athletes.EnumerateArray())
                        {
                            var athleteId = a.TryGetProperty("athlete", out var ath) && ath.TryGetProperty("id", out var aid)
                                ? aid.GetString() ?? "" : "";
                            if (string.IsNullOrEmpty(athleteId))
                                continue;

                            var stats = a.TryGetProperty("stats", out var st) && st.ValueKind == JsonValueKind.Array
                                ? st.EnumerateArray().Select(s => s.GetString() ?? "").ToArray()
                                : Array.Empty<string>();

                            if (!lines.TryGetValue(athleteId, out var line))
                            {
                                line = new FantasyStatLine();
                                lines[athleteId] = line;
                            }

                            ApplyCategory(line, catName, labels, stats);
                        }
                    }

                    foreach (var kvp in lines)
                        result[kvp.Key] = _scoring.ScoreOffense(kvp.Value);
                }
            }
        }

        private static void ApplyCategory(FantasyStatLine line, string category, string[] labels, string[] stats)
        {
            double Stat(string label)
            {
                var idx = Array.FindIndex(labels, l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));
                if (idx < 0 || idx >= stats.Length) return 0;
                var raw = stats[idx];
                // Some stats are formatted like "24/33" (completions/attempts) - take leading number.
                var slash = raw.IndexOf('/');
                if (slash > 0) raw = raw[..slash];
                return double.TryParse(raw, out var v) ? v : 0;
            }

            switch (category.ToLowerInvariant())
            {
                case "passing":
                    line.PassingYards += Stat("YDS");
                    line.PassingTouchdowns += (int)Stat("TD");
                    line.Interceptions += (int)Stat("INT");
                    break;
                case "rushing":
                    line.RushingYards += Stat("YDS");
                    line.RushingTouchdowns += (int)Stat("TD");
                    break;
                case "receiving":
                    line.ReceivingYards += Stat("YDS");
                    line.ReceivingTouchdowns += (int)Stat("TD");
                    line.Receptions += (int)Stat("REC");
                    break;
                case "fumbles":
                    line.FumblesLost += (int)Stat("LOST");
                    break;
            }
        }
    }
}
