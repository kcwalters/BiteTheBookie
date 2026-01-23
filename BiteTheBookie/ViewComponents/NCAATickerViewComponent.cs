using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.ViewComponents
{
    public class NCAATickerViewComponent : ViewComponent
    {
        private readonly INCAAScoresService _scoresService;

        public NCAATickerViewComponent(INCAAScoresService scoresService)
        {
            _scoresService = scoresService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var games = await _scoresService.GetGamesAsync();
            return View(games); // expects Views/Shared/Components/NCAATicker/Default.cshtml
        }
    }
}
