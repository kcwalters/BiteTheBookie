using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Shared helper for the league ticker scores services. Fetches the ESPN scoreboard
    /// for today and, when the league has no games today (off-day or out of season),
    /// falls forward to the next date within a look-ahead window that actually has games.
    /// This keeps every ticker populated year-round instead of going blank off-season.
    /// </summary>
    internal static class EspnScoreboardFetcher
    {
        private const int LookAheadDays = 120;

        /// <summary>
        /// Returns the ESPN scoreboard events for today, or — when today is empty — the
        /// events for the soonest upcoming day within the look-ahead window. Returned
        /// <see cref="JsonElement"/> values are cloned so they remain valid after the
        /// underlying <see cref="JsonDocument"/> is disposed.
        /// </summary>
        public static async Task<List<JsonElement>> GetEarliestDayEventsAsync(
            HttpClient http, string scoreboardUrl, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;

            // 1) Try today first (cheap, most common case for in-season leagues).
            var todays = await FetchEventsAsync(
                http, $"{scoreboardUrl}?dates={today:yyyyMMdd}", cancellationToken);
            if (todays.Count > 0)
            {
                return todays.Select(e => e.Element).ToList();
            }

            // 2) Off-day / offseason: widen to a date range and keep the earliest game day.
            var rangeUrl = $"{scoreboardUrl}?dates={today:yyyyMMdd}-{today.AddDays(LookAheadDays):yyyyMMdd}&limit=100";
            var range = await FetchEventsAsync(http, rangeUrl, cancellationToken);
            if (range.Count == 0)
            {
                return new List<JsonElement>();
            }

            var earliestDay = range.Min(e => e.Date.Date);
            return range
                .Where(e => e.Date.Date == earliestDay)
                .OrderBy(e => e.Date)
                .Select(e => e.Element)
                .ToList();
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
