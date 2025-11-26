using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services.Interfaces;
namespace BiteTheBookie.Controllers
{
    public class OddsController : Controller
    {
        private readonly IOddsService _service;
        public OddsController(IOddsService service) => _service = service;

        public async Task<IActionResult> HeroTicker()
            => PartialView("_HeroTicker", await _service.GetHeroOddsAsync());

        public async Task<IActionResult> LiveWidget()
            => PartialView("_LiveOddsWidget", await _service.GetLiveOddsAsync());

        public async Task<IActionResult> LeagueTabs()
            => PartialView("_LeagueOddsTabs", await _service.GetLeagueOddsAsync());
    }
}
