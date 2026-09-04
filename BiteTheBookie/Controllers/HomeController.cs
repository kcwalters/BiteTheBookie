using System.Diagnostics;
using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Controllers
{
    public class HomeController : Controller
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        private readonly IOddsService _odds;
        private readonly INewsService _news;
        private readonly IMLBGamesService _mlbService;
        private readonly ApplicationDbContext _db;

        public HomeController(IOddsService odds,
                              INewsService news,
                              IMLBGamesService mlbService,
                              ApplicationDbContext db)
        {
            _odds = odds;
            _news = news;
            _mlbService = mlbService;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var heroOddsTask = WithTimeout(() => _odds.GetHeroOddsAsync());
            var newsTask = WithTimeout(() => _news.GetLatestNewsAsync());
            var liveOddsTask = WithTimeout(() => _odds.GetLiveOddsAsync());
            var leagueOddsTask = WithTimeout(() => _odds.GetLeagueOddsAsync());

            await Task.WhenAll(heroOddsTask, newsTask, liveOddsTask, leagueOddsTask);

            // DbContext is not thread-safe, so the database-backed reads run
            // sequentially rather than inside the Task.WhenAll above.
            var expertPicks = await GetRecentExpertPicksAsync();
            var videos = await GetPublishedVideosAsync();

            var vm = new HomePageViewModel
            {
                HeroOdds = heroOddsTask.Result,
                NewsFeed = newsTask.Result,
                LiveOdds = liveOddsTask.Result,
                LeagueOdds = leagueOddsTask.Result,
                ExpertPicks = expertPicks,
                FeaturedVideo = videos.FirstOrDefault(v => v.IsFeatured) ?? videos.FirstOrDefault(),
                RecentVideos = videos
            };

            return View(vm);
        }

        public async Task<IActionResult> MlbScheduleScore()
        {
            var games = await _mlbService.GetTodayGamesAsync();
            return View(games);
        }

        private async Task<IReadOnlyList<ExpertPickSummary>> GetRecentExpertPicksAsync()
        {
            try
            {
                return await _db.ExpertPicks
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(6)
                    .Select(p => new ExpertPickSummary
                    {
                        Id = p.Id,
                        GameId = p.GameId,
                        League = p.League,
                        AwayTeamName = p.AwayTeamName,
                        HomeTeamName = p.HomeTeamName,
                        GameTime = p.GameTime,
                        PickType = p.PickType,
                        PickSelection = p.PickSelection,
                        Confidence = p.Confidence,
                        Analysis = p.Analysis,
                        EnteredBy = p.EnteredBy,
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync();
            }
            catch
            {
                return new List<ExpertPickSummary>();
            }
        }

        private async Task<IReadOnlyList<VideoListItemViewModel>> GetPublishedVideosAsync()
        {
            try
            {
                return await _db.SiteVideos
                    .Where(v => v.IsPublished)
                    .OrderBy(v => v.SortOrder)
                    .ThenByDescending(v => v.CreatedAt)
                    .Take(9)
                    .Select(v => new VideoListItemViewModel
                    {
                        Id = v.Id,
                        Title = v.Title,
                        Description = v.Description,
                        YouTubeId = v.YouTubeId,
                        Category = v.Category,
                        IsPublished = v.IsPublished,
                        IsFeatured = v.IsFeatured,
                        SortOrder = v.SortOrder,
                        EnteredBy = v.EnteredBy,
                        CreatedAt = v.CreatedAt
                    })
                    .ToListAsync();
            }
            catch
            {
                return new List<VideoListItemViewModel>();
            }
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
