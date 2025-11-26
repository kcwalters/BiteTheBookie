using System.Diagnostics;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class HomeController : Controller
    {
        private readonly IOddsService _odds;
        private readonly INewsService _news;
        private readonly IBetSlipService _betSlip;
        private readonly IMlbService _mlbService;

        public HomeController(IOddsService odds,
                              INewsService news,
                              IBetSlipService betSlip,
                              IMlbService mlbService)
        {
            _odds = odds;
            _news = news;
            _betSlip = betSlip;
            _mlbService = mlbService;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new HomePageViewModel
            {
                HeroOdds = await _odds.GetHeroOddsAsync(),
                NewsFeed = await _news.GetLatestNewsAsync(),
                LiveOdds = await _odds.GetLiveOddsAsync(),
                LeagueOdds = await _odds.GetLeagueOddsAsync(),
                BetSlip = await _betSlip.GetBetSlipAsync()
            };
            return View(vm);
        }

        public async Task<IActionResult> MlbScheduleScore()
        {
            var games = await _mlbService.GetTodayGamesAsync();
            return View(games);
        }
    }
}
