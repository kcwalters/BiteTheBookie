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

    public async Task<IViewComponentResult> InvokeAsync(int count = 10)
    {
        var items = await _news.GetLatestNewsAsync(count);
        return View(items);
    }
}
