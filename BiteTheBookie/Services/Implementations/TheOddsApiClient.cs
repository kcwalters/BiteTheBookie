using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BiteTheBookie.Services.Implementations
{
    public class TheOddsApiClient
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly OddsApiOptions _options;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public TheOddsApiClient(HttpClient http, IMemoryCache cache, IOptions<OddsApiOptions> options)
        {
            _http = http;
            _cache = cache;
            _options = options.Value;

            if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                _http.BaseAddress = new Uri(_options.BaseUrl, UriKind.Absolute);
            }
        }

        public async Task<JsonElement> GetAsync(string pathAndQuery, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException("Odds API key is not configured. Set OddsApi:ApiKey.");
            }

            var cacheKey = "oddsapi:" + pathAndQuery;
            if (_cache.TryGetValue(cacheKey, out JsonElement cached))
            {
                return cached;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Odds API request unauthorized. Check OddsApi:ApiKey.");
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement.Clone();

            _cache.Set(cacheKey, root, TimeSpan.FromSeconds(Math.Max(5, _options.CacheSeconds)));
            return root;
        }
    }
}
