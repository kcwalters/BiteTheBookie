using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services;
using BiteTheBookie.Services.Interfaces;
namespace BiteTheBookie.ViewComponents
{
    public class NFLTickerViewComponent : ViewComponent
    {
        private readonly INFLScoresService _scoresService;
        public NFLTickerViewComponent(INFLScoresService scoresService)
        {
            _scoresService = scoresService;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var games = await _scoresService.GetGamesAsync();
            return View(games);
        }
    }
} 
