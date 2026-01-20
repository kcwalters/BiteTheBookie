using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.ViewComponents
{
    public class NHLTickerViewComponent : ViewComponent
    {
        private readonly INHLScoresService _scoresService;

        public NHLTickerViewComponent(INHLScoresService scoresService)
        {
            _scoresService = scoresService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var games = await _scoresService.GetGamesAsync();
            return View(games); // expects Views/Shared/Components/NHLTicker/Default.cshtml
        }
    }
}

