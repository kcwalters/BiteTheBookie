using System.Xml.Linq;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BiteTheBookie.Services.Implementations;

public class EspnRssNewsService : INewsService
{
    private const string CacheKey = "news:espn:rss";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly EspnNewsOptions _options;

    public EspnRssNewsService(HttpClient http, IMemoryCache cache, IOptions<EspnNewsOptions> options)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
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

    public async Task<NewsItemViewModel> GetNewsByIdAsync(int id)
    {
        var items = (await GetLatestNewsAsync(_options.MaxItems)).ToList();
        return items.FirstOrDefault(x => x.Id == id) ?? new NewsItemViewModel();
    }

    private async Task<List<NewsItemViewModel>> FetchAsync(string url, int maxItems)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("BiteTheBookie/1.0");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("ESPN RSS feed returned {StatusCode} for URL {Url}", resp.StatusCode, url);
                return new List<NewsItemViewModel>();
            }

            await using var stream = await resp.Content.ReadAsStreamAsync();
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);

            var channel = doc.Root?.Element("channel");
            var itemEls = channel?.Elements("item") ?? Enumerable.Empty<XElement>();

            var results = new List<NewsItemViewModel>();
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

                // ESPN RSS does not always provide media:content. Leave blank; view can handle it.
                results.Add(new NewsItemViewModel
                {
                    Id = ++i,
                    Title = title,
                    Excerpt = desc,
                    ArticleUrl = link,
                    ImageUrl = string.Empty,
                    PublishedAt = publishedAt
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching or parsing ESPN RSS feed from URL {Url}", url);
            return new List<NewsItemViewModel>();
        }
    }
}
