using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class ScoresController : Controller
    {
        private readonly INFLScoresService _nFlScoresService;
        private readonly INBAScoresService _nBAScoresService;
        private readonly INHLScoresService _nHLScoresService;
        private readonly INCAAScoresService _nCAAScoresService;

        public ScoresController(
            INFLScoresService nFLScoresService,
            INBAScoresService nBAScoresService,
            INHLScoresService nHLSScoresService,
            INCAAScoresService nCAAScoresService)
        {
            _nFlScoresService = nFLScoresService;
            _nBAScoresService = nBAScoresService;
            _nHLScoresService = nHLSScoresService;
            _nCAAScoresService = nCAAScoresService;
        }

        [HttpGet]
        public async Task<IActionResult> NFLTickerInner()
        {
            var games = await _nFlScoresService.GetGamesAsync();
            return PartialView("_NFLTickerInner", games);
        }

        [HttpGet]
        public async Task<IActionResult> NBATickerInner()
        {
            var games = await _nBAScoresService.GetGamesAsync();
            return PartialView("_NBATickerInner", games);
        }

        [HttpGet]
        public async Task<IActionResult> NHLTickerInner()
        {
            var games = await _nHLScoresService.GetGamesAsync();
            return PartialView("_NHLTickerInner", games);
        }

        [HttpGet]
        public async Task<IActionResult> NCAATickerInner()
        {
            var games = await _nCAAScoresService.GetGamesAsync();
            return PartialView("_NCAATickerInner", games);
        }
    }
}

