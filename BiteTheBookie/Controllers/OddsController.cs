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

        public async Task<IActionResult> NBA(CancellationToken cancellationToken)
        {
            var nbaOdds = await _service.GetNBAOddsAsync(cancellationToken);
            return View(nbaOdds);
        }

        public async Task<IActionResult> CBB(CancellationToken cancellationToken)
        {
            var cbbOdds = await _service.GetCBBOddsAsync(cancellationToken);
            return View(cbbOdds);
        }

        public async Task<IActionResult> NCAA(CancellationToken cancellationToken)
        {
            var cbbOdds = await _service.GetCBBOddsAsync(cancellationToken);
            return View("CBB", cbbOdds);
        }

        public async Task<IActionResult> MLB(CancellationToken cancellationToken)
        {
            var mlbOdds = await _service.GetMLBOddsAsync(cancellationToken);
            return View(mlbOdds);
        }

        public async Task<IActionResult> NHL(CancellationToken cancellationToken)
        {
            var nhlOdds = await _service.GetNHLOddsAsync(cancellationToken);
            return View(nhlOdds);
        }

        public async Task<IActionResult> HeroTicker()
            => PartialView("_HeroTicker", await _service.GetHeroOddsAsync());

        public async Task<IActionResult> LiveWidget()
            => PartialView("_LiveOddsWidget", await _service.GetLiveOddsAsync());

        public async Task<IActionResult> LeagueTabs()
            => PartialView("_LeagueOddsTabs", await _service.GetLeagueOddsAsync());
    }
}
