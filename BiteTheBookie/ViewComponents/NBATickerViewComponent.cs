using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.ViewComponents
{
    public class NBATickerViewComponent : ViewComponent
    {
        private readonly INBAScoresService _scoresService;

        public NBATickerViewComponent(INBAScoresService scoresService)
        {
            _scoresService = scoresService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var games = await _scoresService.GetGamesAsync();
            return View(games); // expects Views/Shared/Components/NBATicker/Default.cshtml
        }
    }
}

