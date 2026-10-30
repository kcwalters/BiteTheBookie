using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Shared helper for the league ticker scores services. Fetches the ESPN scoreboard
    /// for today and, when the league has no games today (off-day), falls forward to the
    /// next date within a short look-ahead window that actually has games. When a sport is
    /// out of season (no games within the window) an empty list is returned so the ticker
    /// stays hidden instead of showing a game weeks or months away.
    /// </summary>
    internal static class EspnScoreboardFetcher
    {
        // Short window so in-season off-days still populate the ticker, but out-of-season
        // leagues (whose next game is further out) return empty and are hidden.
        private const int LookAheadDays = 10;

        /// <summary>
        /// Returns the ESPN scoreboard events for today, or — when today is empty — the
        /// events for the soonest upcoming day within the look-ahead window. Returned
        /// <see cref="JsonElement"/> values are cloned so they remain valid after the
        /// underlying <see cref="JsonDocument"/> is disposed.
        /// </summary>
        public static async Task<List<JsonElement>> GetEarliestDayEventsAsync(
            HttpClient http, string scoreboardUrl, CancellationToken cancellationToken, string? extraQuery = null, int? lookAheadDays = null)
        {
            var today = DateTime.UtcNow.Date;
            var suffix = string.IsNullOrWhiteSpace(extraQuery) ? string.Empty : $"&{extraQuery}";
            var window = lookAheadDays ?? LookAheadDays;

            // Walk forward one day at a time and return the first day that has games.
            // The ESPN scoreboard API only accepts a single "dates=yyyyMMdd" value; the
            // "dates=start-end" range format responds with HTTP 400, so we probe each day
            // within the look-ahead window instead. Today is checked first (the common
            // in-season case); if the league has no games within the window an empty list
            // is returned so the ticker stays hidden.
            for (var offset = 0; offset <= window; offset++)
            {
                var day = today.AddDays(offset);
                var events = await FetchEventsAsync(
                    http, $"{scoreboardUrl}?dates={day:yyyyMMdd}{suffix}", cancellationToken);
                if (events.Count > 0)
                {
                    return events
                        .OrderBy(e => e.Date)
                        .Select(e => e.Element)
                        .ToList();
                }
            }

            return new List<JsonElement>();
        }

        public static async Task<List<JsonElement>> GetEventsForDateAsync(
            HttpClient http, string baseUrl, DateTime date, CancellationToken cancellationToken)
        {
            if (date.Date > DateTime.UtcNow.Date)
            {
                var nextDayUrl = baseUrl + $"?date={date:yyyyMMdd}";
                return (await FetchEventsAsync(http, nextDayUrl, cancellationToken)).Select(e => e.Element).ToList();
            }

            return (await FetchEventsAsync(http, baseUrl, cancellationToken)).Select(e => e.Element).ToList();
        }

        private static async Task<List<(JsonElement Element, DateTime Date)>> FetchEventsAsync(
            HttpClient http, string url, CancellationToken cancellationToken)
        {
            var results = new List<(JsonElement, DateTime)>();
            try
            {
                using var response = await http.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (!root.TryGetProperty("events", out var events) ||
                    events.ValueKind != JsonValueKind.Array)
                {
                    return results;
                }

                foreach (var ev in events.EnumerateArray())
                {
                    var date = ev.TryGetProperty("date", out var dateEl) &&
                               dateEl.TryGetDateTime(out var parsed)
                        ? parsed.ToUniversalTime()
                        : DateTime.UtcNow;

                    // Clone so the element survives disposal of the JsonDocument.
                    results.Add((ev.Clone(), date));
                }
            }
            catch
            {
                // Swallow — callers treat an empty list as "no games", which is correct
                // for both transient failures and genuinely empty scoreboards.
            }

            return results;
        }
    }
}
