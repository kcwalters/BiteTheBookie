using System.Text.Json;
using System.Xml.Linq;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BiteTheBookie.Services.Implementations;

public class EspnRssNewsService : INewsService
{
    private const string CacheKey = "news:espn:rss";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly EspnNewsOptions _options;
    private readonly ILogger<EspnRssNewsService> _logger;

    public EspnRssNewsService(HttpClient http, IMemoryCache cache, IOptions<EspnNewsOptions> options, ILogger<EspnRssNewsService> logger)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<NewsItemViewModel>> GetLatestNewsAsync(int count = 5)
    {
        var max = Math.Max(1, Math.Min(count, _options.MaxItems));

        if (_cache.TryGetValue(CacheKey, out List<NewsItemViewModel>? cached) && cached is { Count: > 0 })
            return cached.Take(max);

        var items = await FetchAsync(_options.FeedUrl, _options.MaxItems);

        _cache.Set(CacheKey, items, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(5, _options.CacheSeconds))
        });

        return items.Take(max);
    }

    public async Task<IEnumerable<NewsItemViewModel>> GetLatestNewsAsync(string feedUrl, int count)
    {
        if (string.IsNullOrWhiteSpace(feedUrl))
            return await GetLatestNewsAsync(count);

        var max = Math.Max(1, count);
        var cacheKey = $"news:espn:rss:{feedUrl}";

        if (_cache.TryGetValue(cacheKey, out List<NewsItemViewModel>? cached) && cached is { Count: > 0 })
            return cached.Take(max);

        var items = await FetchAsync(feedUrl, Math.Max(max, _options.MaxItems));

        _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(5, _options.CacheSeconds))
        });

        return items.Take(max);
    }

    public async Task<NewsItemViewModel> GetNewsByIdAsync(int id)
    {
        var items = (await GetLatestNewsAsync(_options.MaxItems)).ToList();
        return items.FirstOrDefault(x => x.Id == id) ?? new NewsItemViewModel();
    }

    private async Task<List<NewsItemViewModel>> FetchAsync(string url, int maxItems)
    {
        // ESPN blocks the espn.com RSS host (Akamai "Access Denied"), so those feeds return
        // empty. The JSON news API on site.web.api.espn.com is reachable (same host as the
        // scoreboard), so prefer it and fall back to RSS/XML parsing when a raw feed is requested.
        var apiUrl = ToJsonApiUrl(url);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            req.Headers.Accept.ParseAdd("application/json, application/rss+xml, application/xml, text/xml");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("ESPN news feed returned {StatusCode} for URL {Url}", resp.StatusCode, apiUrl);
                return new List<NewsItemViewModel>();
            }

            var raw = await resp.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning("ESPN news feed returned an empty body from {Url}", apiUrl);
                return new List<NewsItemViewModel>();
            }

            var trimmed = raw.TrimStart();

            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
                return ParseJson(raw, maxItems);

            if (trimmed.StartsWith('<'))
                return ParseRss(raw, maxItems);

            _logger.LogWarning("ESPN news feed returned an unexpected body from {Url}. Preview: {Preview}",
                apiUrl, raw.Length > 200 ? raw[..200] : raw);
            return new List<NewsItemViewModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching or parsing ESPN news feed from URL {Url}", apiUrl);
            return new List<NewsItemViewModel>();
        }
    }

    private List<NewsItemViewModel> ParseJson(string raw, int maxItems)
    {
        var results = new List<NewsItemViewModel>();

        using var doc = JsonDocument.Parse(raw);

        if (!doc.RootElement.TryGetProperty("articles", out var articles) ||
            articles.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        var i = 0;
        foreach (var article in articles.EnumerateArray().Take(Math.Max(1, maxItems)))
        {
            var title = article.TryGetProperty("headline", out var h) ? h.GetString() ?? string.Empty : string.Empty;
            var desc = article.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;

            var publishedAt = DateTime.UtcNow;
            if (article.TryGetProperty("published", out var p) &&
                DateTimeOffset.TryParse(p.GetString(), out var dto))
            {
                publishedAt = dto.UtcDateTime;
            }

            var link = string.Empty;
            if (article.TryGetProperty("links", out var links) &&
                links.TryGetProperty("web", out var web) &&
                web.TryGetProperty("href", out var href))
            {
                link = href.GetString() ?? string.Empty;
            }

            var imageUrl = string.Empty;
            if (article.TryGetProperty("images", out var images) &&
                images.ValueKind == JsonValueKind.Array &&
                images.GetArrayLength() > 0 &&
                images[0].TryGetProperty("url", out var imgUrl))
            {
                imageUrl = imgUrl.GetString() ?? string.Empty;
            }

            results.Add(new NewsItemViewModel
            {
                Id = ++i,
                Title = title.Trim(),
                Excerpt = desc.Trim(),
                ArticleUrl = EnsureHttps(link),
                ImageUrl = EnsureHttps(imageUrl),
                PublishedAt = publishedAt
            });
        }

        return results;
    }

    private static List<NewsItemViewModel> ParseRss(string raw, int maxItems)
    {
        var results = new List<NewsItemViewModel>();

        var doc = XDocument.Parse(raw);
        var channel = doc.Root?.Element("channel");
        var itemEls = channel?.Elements("item") ?? Enumerable.Empty<XElement>();

        XNamespace media = "http://search.yahoo.com/mrss/";

        var i = 0;
        foreach (var el in itemEls.Take(Math.Max(1, maxItems)))
        {
            var title = el.Element("title")?.Value?.Trim() ?? string.Empty;
            var link = el.Element("link")?.Value?.Trim() ?? string.Empty;
            var desc = el.Element("description")?.Value?.Trim() ?? string.Empty;
            var pubDateRaw = el.Element("pubDate")?.Value?.Trim() ?? string.Empty;

            var publishedAt = DateTime.UtcNow;
            if (DateTimeOffset.TryParse(pubDateRaw, out var dto))
                publishedAt = dto.UtcDateTime;

            var imageUrl =
                el.Element(media + "content")?.Attribute("url")?.Value ??
                el.Element(media + "thumbnail")?.Attribute("url")?.Value ??
                el.Element("enclosure")?.Attribute("url")?.Value ??
                string.Empty;

            results.Add(new NewsItemViewModel
            {
                Id = ++i,
                Title = title,
                Excerpt = desc,
                ArticleUrl = EnsureHttps(link),
                ImageUrl = EnsureHttps(imageUrl),
                PublishedAt = publishedAt
            });
        }

        return results;
    }

    // Maps ESPN RSS feed URLs (Akamai-blocked host) to the JSON news API on the
    // site.web.api.espn.com host (the same reachable host used by the scoreboard).
    // e.g. https://www.espn.com/espn/rss/nfl/news -> https://site.web.api.espn.com/apis/site/v2/sports/football/nfl/news
    private static string ToJsonApiUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        var map = new (string Segment, string Sport, string League)[]
        {
            ("/nfl/", "football", "nfl"),
            ("/nba/", "basketball", "nba"),
            ("/mlb/", "baseball", "mlb"),
            ("/nhl/", "hockey", "nhl"),
            ("/ncf/", "football", "college-football"),
            ("/ncb/", "basketball", "mens-college-basketball"),
        };

        if (url.Contains("/rss/", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (segment, sport, league) in map)
            {
                if (url.Contains(segment, StringComparison.OrdinalIgnoreCase))
                    return $"https://site.web.api.espn.com/apis/site/v2/sports/{sport}/{league}/news";
            }

            // Generic ESPN RSS news feed -> top NFL news as a sensible default.
            return "https://site.web.api.espn.com/apis/site/v2/sports/football/nfl/news";
        }

        return url;
    }

    // ESPN feeds can return http:// URLs. Upgrade them to https:// so images
    // and links are not blocked as mixed content on the https production site.
    private static string EnsureHttps(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        url = url.Trim();

        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? string.Concat("https://", url.AsSpan("http://".Length))
            : url;
    }
}
