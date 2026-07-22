using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.ViewComponents
{
    public class CFBTickerViewComponent : ViewComponent
    {
        private readonly ICFBScoresService _scoresService;

        public CFBTickerViewComponent(ICFBScoresService scoresService)
        {
            _scoresService = scoresService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var games = await _scoresService.GetGamesAsync();
            return View(games); // expects Views/Shared/Components/CFBTicker/Default.cshtml
        }
    }
}
