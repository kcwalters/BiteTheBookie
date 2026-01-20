using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class ScoresController : Controller
    {
        private readonly INFLScoresService _nFlScoresService;
        private readonly INBAScoresService _nBAScoresService;
        private readonly INHLScoresService _nHLScoresService;

        public ScoresController(INFLScoresService nFLScoresService, INBAScoresService nBAScoresService, INHLScoresService nHLSScoresService)
        {
            _nFlScoresService = nFLScoresService;
            _nBAScoresService = nBAScoresService;
            _nHLScoresService = nHLSScoresService;
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
    }
}

