using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.ViewComponents;

public class EspnNewsFeedViewComponent : ViewComponent
{
    private readonly INewsService _news;

    public EspnNewsFeedViewComponent(INewsService news)
    {
        _news = news;
    }

    public async Task<IViewComponentResult> InvokeAsync(int count = 10, string league = "")
    {
        var feedUrl = ResolveFeedUrl(league);

        var items = string.IsNullOrEmpty(feedUrl)
            ? await _news.GetLatestNewsAsync(count)
            : await _news.GetLatestNewsAsync(feedUrl, count);

        ViewData["NewsTitle"] = string.IsNullOrWhiteSpace(league)
            ? "ESPN Headlines"
            : $"{league.Trim().ToUpperInvariant()} Headlines";

        return View(items);
    }

    // Maps a league code to its ESPN news RSS feed. Returns empty for unknown leagues
    // so the component falls back to the default configured feed.
    private static string ResolveFeedUrl(string league) => (league ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "NFL" => "https://www.espn.com/espn/rss/nfl/news",
        "NBA" => "https://www.espn.com/espn/rss/nba/news",
        "MLB" => "https://www.espn.com/espn/rss/mlb/news",
        "NHL" => "https://www.espn.com/espn/rss/nhl/news",
        "CFB" => "https://www.espn.com/espn/rss/ncf/news",
        "CBB" => "https://www.espn.com/espn/rss/ncb/news",
        _ => string.Empty
    };
}
