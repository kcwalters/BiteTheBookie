using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;

namespace BiteTheBookie.Controllers
{
    public class OddsController : Controller
    {
        private readonly IOddsService _service;
        public OddsController(IOddsService service) => _service = service;

        public async Task<IActionResult> Index()
        {
            var vm = new HomePageViewModel
            {
                HeroOdds = await _service.GetHeroOddsAsync(),
                LiveOdds = await _service.GetLiveOddsAsync(),
                LeagueOdds = await _service.GetLeagueOddsAsync()
            };

            return View(vm);
        }

        public async Task<IActionResult> NFL(CancellationToken cancellationToken)
        {
            var nflOdds = await _service.GetNFLOddsAsync(cancellationToken);
            return View(nflOdds);
        }

        public async Task<IActionResult> HeroTicker()
            => PartialView("_HeroTicker", await _service.GetHeroOddsAsync());

        public async Task<IActionResult> LiveWidget()
            => PartialView("_LiveOddsWidget", await _service.GetLiveOddsAsync());

        public async Task<IActionResult> LeagueTabs()
            => PartialView("_LeagueOddsTabs", await _service.GetLeagueOddsAsync());
    }
}
