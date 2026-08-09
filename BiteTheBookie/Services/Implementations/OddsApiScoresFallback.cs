using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>A provider-agnostic parsed game used when ESPN is unavailable.</summary>
    internal readonly record struct FallbackGame(
        string Id,
        string AwayTeam,
        string HomeTeam,
        int? AwayScore,
        int? HomeScore,
        DateTimeOffset CommenceTime,
        bool Completed);

    /// <summary>
    /// Fetches scores from The Odds API as a fallback when ESPN's site API returns 403.
    /// Maps our league codes to Odds API sport keys and parses the <c>/scores</c> payload.
    /// </summary>
    internal static class OddsApiScoresFallback
    {
        // Our league code -> Odds API sport key.
        private static readonly Dictionary<string, string> SportKeys =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["NFL"] = "americanfootball_nfl",
            ["NBA"] = "basketball_nba",
            ["NHL"] = "icehockey_nhl",
            ["MLB"] = "baseball_mlb",
            ["CFB"] = "americanfootball_ncaaf",
            ["CBB"] = "basketball_ncaab",
            ["NCAA"] = "basketball_ncaab",
        };

        public static string? GetSportKey(string league) =>
            SportKeys.TryGetValue(league ?? string.Empty, out var key) ? key : null;

        /// <summary>
        /// Fetches games for a league, merging recent/live/completed games from /scores
        /// with upcoming scheduled games from /events, ordered by start time. This lets
        /// both the ticker (recent) and the date-based schedule (future) populate.
        /// When <paramref name="targetDate"/> is supplied, the /events query is widened to
        /// include that calendar day so future dates beyond the default window still load.
        /// </summary>
        public static async Task<List<FallbackGame>> GetGamesAsync(
            TheOddsApiClient client, string league, CancellationToken cancellationToken,
            DateTime? targetDate = null)
        {
            var sportKey = GetSportKey(league);
            if (sportKey is null)
            {
                return new List<FallbackGame>();
            }

            // When a specific future date is requested, ask the Odds API for events across
            // a window that spans from now through the day AFTER the target (in UTC) so the
            // selected day's games are returned even if they fall outside the default window.
            DateTimeOffset? from = null, to = null;
            if (targetDate.HasValue)
            {
                var startUtc = DateTime.SpecifyKind(targetDate.Value.Date, DateTimeKind.Utc).AddDays(-1);
                var nowUtc = DateTime.UtcNow.Date;
                from = new DateTimeOffset(startUtc < nowUtc ? nowUtc : startUtc, TimeSpan.Zero);
                to = new DateTimeOffset(
                    DateTime.SpecifyKind(targetDate.Value.Date, DateTimeKind.Utc).AddDays(2), TimeSpan.Zero);
            }

            var scores = Parse(await client.GetScoresAsync(sportKey, daysFrom: 3, cancellationToken: cancellationToken));
            var events = Parse(await client.GetEventsAsync(sportKey, cancellationToken, from, to));

            // Merge, de-duplicating by game id (scores take priority since they carry results).
            var byId = new Dictionary<string, FallbackGame>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in events)
            {
                if (!string.IsNullOrEmpty(g.Id)) byId[g.Id] = g;
            }
            foreach (var g in scores)
            {
                if (!string.IsNullOrEmpty(g.Id)) byId[g.Id] = g;
            }

            return byId.Values.OrderBy(g => g.CommenceTime).ToList();
        }


        private static List<FallbackGame> Parse(JsonElement root)
        {
            var games = new List<FallbackGame>();
            if (root.ValueKind != JsonValueKind.Array)
            {
                return games;
            }

            foreach (var ev in root.EnumerateArray())
            {
                try
                {
                    var id = ev.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    var home = ev.TryGetProperty("home_team", out var hEl) ? hEl.GetString() ?? "" : "";
                    var away = ev.TryGetProperty("away_team", out var aEl) ? aEl.GetString() ?? "" : "";
                    var completed = ev.TryGetProperty("completed", out var cEl) &&
                                    cEl.ValueKind == JsonValueKind.True;

                    DateTimeOffset commence = default;
                    if (ev.TryGetProperty("commence_time", out var ctEl) &&
                        ctEl.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(ctEl.GetString(), out var parsed))
                    {
                        commence = parsed;
                    }

                    int? homeScore = null, awayScore = null;
                    if (ev.TryGetProperty("scores", out var scoresEl) &&
                        scoresEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in scoresEl.EnumerateArray())
                        {
                            var name = s.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                            var scoreStr = s.TryGetProperty("score", out var scEl) ? scEl.GetString() : null;
                            if (int.TryParse(scoreStr, out var val))
                            {
                                if (string.Equals(name, home, StringComparison.OrdinalIgnoreCase)) homeScore = val;
                                else if (string.Equals(name, away, StringComparison.OrdinalIgnoreCase)) awayScore = val;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(away))
                    {
                        games.Add(new FallbackGame(id, away, home, awayScore, homeScore, commence, completed));
                    }
                }
                catch
                {
                    // Skip malformed entries.
                }
            }

            return games.OrderBy(g => g.CommenceTime).ToList();
        }

        /// <summary>Human-friendly status text for a fallback game.</summary>
        public static string StatusText(FallbackGame g)
        {
            if (g.Completed) return "Final";
            if (g.AwayScore.HasValue || g.HomeScore.HasValue) return "Live";
            return g.CommenceTime == default
                ? "Scheduled"
                : g.CommenceTime.ToLocalTime().ToString("MMM d, h:mm tt");
        }

        /// <summary>Simple status label ("Final"/"Live"/"Scheduled") for schedule rows.</summary>
        public static string StatusLabel(FallbackGame g)
        {
            if (g.Completed) return "Final";
            if (g.AwayScore.HasValue || g.HomeScore.HasValue) return "Live";
            return "Scheduled";
        }

        /// <summary>
        /// Builds a date-filtered schedule for the Scores &amp; Simulations page from
        /// The Odds API. When <paramref name="date"/> is provided, only games on that
        /// local calendar day are returned; otherwise all fetched games are returned.
        /// When <paramref name="fallbackToNext"/> is true and no games fall on the
        /// selected date, the soonest upcoming date's games are returned instead so the
        /// page always shows the next scheduled game (e.g. NBA out of season).
        /// </summary>
        public static async Task<List<Models.NBAGameMatchup>> GetScheduleAsync(
            TheOddsApiClient client, string league, DateTime? date, CancellationToken cancellationToken,
            bool fallbackToNext = false)
        {
            // Pass the selected date so the Odds API /events query is widened to cover it,
            // allowing future dates beyond the default upcoming window to load.
            var games = await GetGamesAsync(client, league, cancellationToken, date);

            IEnumerable<FallbackGame> filtered = games;
            if (date.HasValue)
            {
                var target = date.Value.Date;
                filtered = games.Where(g => TickerScheduleHelper.ToEasternDate(g.CommenceTime) == target);

                // When nothing is scheduled on the requested day, optionally fall back to
                // the next upcoming date that has games so the page is never empty.
                if (fallbackToNext && !filtered.Any())
                {
                    var nextDate = games
                        .Where(g => TickerScheduleHelper.ToEasternDate(g.CommenceTime) >= target)
                        .Select(g => TickerScheduleHelper.ToEasternDate(g.CommenceTime))
                        .OrderBy(d => d)
                        .Cast<DateTime?>()
                        .FirstOrDefault();

                    // If there are no games on/after the target (e.g. only past games in the
                    // window), fall back to the soonest game overall.
                    nextDate ??= games
                        .Select(g => TickerScheduleHelper.ToEasternDate(g.CommenceTime))
                        .OrderBy(d => d)
                        .Cast<DateTime?>()
                        .FirstOrDefault();

                    if (nextDate.HasValue)
                    {
                        filtered = games.Where(g =>
                            TickerScheduleHelper.ToEasternDate(g.CommenceTime) == nextDate.Value);
                    }
                }
            }

            return filtered
                .OrderBy(g => g.CommenceTime)
                .Select(g => new Models.NBAGameMatchup
                {
                    GameId = g.Id,
                    AwayTeamName = g.AwayTeam,
                    HomeTeamName = g.HomeTeam,
                    AwayScore = g.AwayScore,
                    HomeScore = g.HomeScore,
                    GameTime = g.CommenceTime.ToLocalTime().DateTime,
                    Status = StatusLabel(g),
                    StatusDetail = StatusText(g)
                }).ToList();
        }
    }
}
