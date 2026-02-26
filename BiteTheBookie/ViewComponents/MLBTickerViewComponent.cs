using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.ViewComponents
{
    public class MLBTickerViewComponent : ViewComponent
    {
        private readonly IMlbService _mlbService;

        public MLBTickerViewComponent(IMlbService mlbService)
        {
            _mlbService = mlbService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var games = await _mlbService.GetTodayGamesAsync();
            return View(games); // expects Views/Shared/Components/MLBTicker/Default.cshtml
        }
    }
}
