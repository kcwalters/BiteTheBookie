using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Shared helpers for the score tickers so they always display the "next game day":
    /// today's games if any are scheduled today, otherwise the closest upcoming date
    /// that has games. Falls back to the most recent past game day when nothing is upcoming.
    /// </summary>
    internal static class TickerScheduleHelper
    {
        private static readonly TimeZoneInfo Eastern = ResolveEastern();

        /// <summary>
        /// Applies a full set of browser-like headers so ESPN's site API does not
        /// reject the request with 403. A bare User-Agent is no longer sufficient.
        /// </summary>
        public static void ApplyBrowserHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            request.Headers.TryAddWithoutValidation("Referer", "https://www.espn.com/");
            request.Headers.TryAddWithoutValidation("Origin", "https://www.espn.com");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        }


        private static TimeZoneInfo ResolveEastern()
        {
            foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.Utc;
        }

        /// <summary>
        /// Builds the ESPN scoreboard query for a date window starting today
        /// (Eastern) and spanning <paramref name="days"/> days, e.g. <c>?dates=20240101-20240115</c>.
        /// </summary>
        public static string BuildDateRangeQuery(int days = 14)
        {
            var todayEastern = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Eastern).Date;
            var end = todayEastern.AddDays(days);
            return $"?dates={todayEastern:yyyyMMdd}-{end:yyyyMMdd}";
        }

        /// <summary>Gets the Eastern-time game day for an ESPN event, or null if unavailable.</summary>
        public static DateTime? GetEventEasternDate(JsonElement ev)
        {
            if (ev.TryGetProperty("date", out var dateEl) &&
                dateEl.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(dateEl.GetString(), out var dto))
            {
                return TimeZoneInfo.ConvertTime(dto, Eastern).Date;
            }
            return null;
        }

        /// <summary>Converts a UTC/absolute instant to its US Eastern calendar date.</summary>
        public static DateTime ToEasternDate(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, Eastern).Date;


        /// <summary>
        /// From a list of (game day, view) picks the games for the "next game day":
        /// the earliest date on/after today that has games, else the most recent past day.
        /// Items without a date are ignored for grouping but returned if nothing else matches.
        /// </summary>
        public static List<T> SelectNextGameDay<T>(List<(DateTime? Date, T View)> items)
        {
            var dated = items.Where(i => i.Date.HasValue).ToList();
            if (dated.Count == 0)
            {
                return items.Select(i => i.View).ToList();
            }

            var todayEastern = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Eastern).Date;

            var target = dated
                .Select(i => i.Date!.Value)
                .Where(d => d >= todayEastern)
                .DefaultIfEmpty(dated.Max(i => i.Date!.Value)) // fallback: most recent past day
                .Min();

            return dated
                .Where(i => i.Date!.Value == target)
                .Select(i => i.View)
                .ToList();
        }
    }
}
