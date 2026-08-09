using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace BiteTheBookie.Services.Implementations
{
    public class TheOddsApiClient
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly OddsApiOptions _options;
        private readonly ILogger<TheOddsApiClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public TheOddsApiClient(HttpClient http, IMemoryCache cache, IOptions<OddsApiOptions> options, ILogger<TheOddsApiClient> logger)
        {
            _http = http;
            _cache = cache;
            _options = options.Value;
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                _http.BaseAddress = new Uri(_options.BaseUrl, UriKind.Absolute);
            }
        }

        public async Task<JsonElement> GetAsync(string pathAndQuery, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_options.ApiKey))
                {
                    _logger.LogError("❌ Odds API key is not configured");
                    throw new InvalidOperationException("Odds API key is not configured. Set OddsApi:ApiKey.");
                }

                // Add API key to the query string
                var separator = pathAndQuery.Contains('?') ? "&" : "?";
                var fullPath = $"{pathAndQuery}{separator}apiKey={_options.ApiKey}";
                
                _logger.LogInformation("🔗 Calling Odds API: {BaseUrl}{Path}", _http.BaseAddress, pathAndQuery);

                var cacheKey = "oddsapi:" + pathAndQuery;
                if (_cache.TryGetValue(cacheKey, out JsonElement cached))
                {
                    _logger.LogInformation("✅ Returning cached response for {Path}", pathAndQuery);
                    return cached;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, fullPath);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");

                using var response = await _http.SendAsync(request, cancellationToken);
                
                _logger.LogInformation("📡 Odds API Response: {StatusCode}", response.StatusCode);
                
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("❌ Odds API unauthorized - check API key");
                    throw new InvalidOperationException("Odds API request unauthorized. Check OddsApi:ApiKey.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("❌ Odds API error {StatusCode}: {Error}", response.StatusCode, errorBody);
                }

                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement.Clone();

                _cache.Set(cacheKey, root, TimeSpan.FromSeconds(Math.Max(5, _options.CacheSeconds)));
                
                _logger.LogInformation("✅ Successfully fetched and cached Odds API data for {Path}", pathAndQuery);
                return root;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error calling Odds API for {Path}", pathAndQuery);
                throw;
            }
        }

        /// <summary>
        /// Fetches recent/live/upcoming scores for a sport from The Odds API
        /// <c>/sports/{sportKey}/scores</c> endpoint (daysFrom=3 includes recently completed games).
        /// Returns an empty array element on failure.
        /// </summary>
        public async Task<JsonElement> GetScoresAsync(string sportKey, int daysFrom = 3, CancellationToken cancellationToken = default)
        {
            try
            {
                return await GetAsync($"sports/{sportKey}/scores?daysFrom={daysFrom}", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Odds API scores fallback failed for {Sport}", sportKey);
                using var empty = JsonDocument.Parse("[]");
                return empty.RootElement.Clone();
            }
        }

        /// <summary>
        /// Fetches upcoming scheduled events for a sport from The Odds API
        /// <c>/sports/{sportKey}/events</c> endpoint. Unlike /scores this returns future
        /// games (no scores) so date-based schedules can populate. Empty array on failure.
        /// When <paramref name="commenceTimeFrom"/>/<paramref name="commenceTimeTo"/> are
        /// supplied, the query is restricted to that UTC window so a specific date can be
        /// fetched rather than only the default upcoming window.
        /// </summary>
        public async Task<JsonElement> GetEventsAsync(
            string sportKey,
            CancellationToken cancellationToken = default,
            DateTimeOffset? commenceTimeFrom = null,
            DateTimeOffset? commenceTimeTo = null)
        {
            try
            {
                // The Odds API requires ISO8601 UTC without fractional seconds (e.g. 2026-01-15T00:00:00Z).
                var query = $"sports/{sportKey}/events";
                var filters = new List<string>();
                if (commenceTimeFrom.HasValue)
                    filters.Add($"commenceTimeFrom={commenceTimeFrom.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
                if (commenceTimeTo.HasValue)
                    filters.Add($"commenceTimeTo={commenceTimeTo.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}");
                if (filters.Count > 0)
                    query += "?" + string.Join("&", filters);

                return await GetAsync(query, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Odds API events fetch failed for {Sport}", sportKey);
                using var empty = JsonDocument.Parse("[]");
                return empty.RootElement.Clone();
            }
        }
    }
}
