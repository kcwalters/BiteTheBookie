using System.Diagnostics;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class HomeController : Controller
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

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
            var heroOddsTask = WithTimeout(() => _odds.GetHeroOddsAsync());
            var newsTask = WithTimeout(() => _news.GetLatestNewsAsync());
            var liveOddsTask = WithTimeout(() => _odds.GetLiveOddsAsync());
            var leagueOddsTask = WithTimeout(() => _odds.GetLeagueOddsAsync());
            var betSlipTask = WithTimeout(() => _betSlip.GetBetSlipAsync());

            await Task.WhenAll(heroOddsTask, newsTask, liveOddsTask, leagueOddsTask, betSlipTask);

            var vm = new HomePageViewModel
            {
                HeroOdds = heroOddsTask.Result,
                NewsFeed = newsTask.Result,
                LiveOdds = liveOddsTask.Result,
                LeagueOdds = leagueOddsTask.Result,
                BetSlip = betSlipTask.Result
            };

            return View(vm);
        }

        public async Task<IActionResult> MlbScheduleScore()
        {
            var games = await _mlbService.GetTodayGamesAsync();
            return View(games);
        }

        private static async Task<T?> WithTimeout<T>(Func<Task<T>> action)
        {
            try
            {
                var actionTask = action();
                var completed = await Task.WhenAny(actionTask, Task.Delay(DefaultTimeout));
                if (completed != actionTask)
                {
                    return default;
                }

                return await actionTask;
            }
            catch
            {
                return default;
            }
        }
    }
}
