using BiteTheBookie.Models;
using BiteTheBookie.Services.Implementations;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class CollegeFootballController : Controller
    {
        private const string NewsFeedUrl = "https://www.espn.com/espn/rss/ncf/news";

        private readonly ICFBScoresService _scoresService;
        private readonly INewsService _newsService;

        public CollegeFootballController(ICFBScoresService scoresService, INewsService newsService)
        {
            _scoresService = scoresService;
            _newsService = newsService;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var model = new LeagueHomeViewModel
            {
                LeagueName = "CFB",
                LeagueLogo = "/img/NCAAMens_med.png",
                TeamController = "CollegeFootball",
                TeamsAction = "Teams",
                OddsController = "Odds",
                OddsAction = "CFB",
                ExpertPicksLeague = "CFB",
                EspnScheduleUrl = "https://www.espn.com/college-football/schedule",
                EspnStandingsUrl = "https://www.espn.com/college-football/standings"
            };

            try
            {
                var games = await _scoresService.GetGamesAsync(cancellationToken);
                model.Games = games.Select(g => new NBAGameMatchup
                {
                    GameId = g.EventId ?? string.Empty,
                    AwayTeamName = g.AwayTeam,
                    AwayTeamLogo = g.AwayLogo,
                    AwayScore = g.AwayScore,
                    HomeTeamName = g.HomeTeam,
                    HomeTeamLogo = g.HomeLogo,
                    HomeScore = g.HomeScore,
                    StatusDetail = g.StatusText,
                    Status = g.IsLive ? "Live" : g.IsFinal ? "Final" : "Scheduled"
                }).ToList();
            }
            catch
            {
                model.ErrorMessage = "Live CFB scores are unavailable right now.";
            }

            try
            {
                model.Headlines = (await _newsService.GetLatestNewsAsync(NewsFeedUrl, 9)).ToList();
            }
            catch
            {
                // Headlines are optional; the view handles an empty list.
            }

            return View("LeagueHome", model);
        }


        // ESPN team IDs (same source as the CFB logo lookup) used to deep-link to each school's page.
        private static string BuildEspnUrl(string code, string name)
        {
            var logo = CFBGamesService.GetTeamInfo(code).Logo; // .../ncaa/500/{id}.png
            var espnId = ExtractEspnId(logo);

            if (string.IsNullOrEmpty(espnId))
                return "https://www.espn.com/college-football/teams";

            var slug = name.ToLowerInvariant()
                .Replace("'", string.Empty)
                .Replace("&", "and")
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", "-");

            return $"https://www.espn.com/college-football/team/_/id/{espnId}/{slug}";
        }

        private static string ExtractEspnId(string logoUrl)
        {
            if (string.IsNullOrEmpty(logoUrl)) return string.Empty;

            var fileName = logoUrl.Split('/').LastOrDefault() ?? string.Empty; // "{id}.png"
            var id = fileName.Replace(".png", string.Empty);
            return id;
        }

        public IActionResult Teams()
        {
            var grouped = CFBGamesService.GetTeamsByConference();

            var viewModel = new CFBTeamsViewModel
            {
                Conferences = grouped.Select(g => new CFBConferenceViewModel
                {
                    Conference = g.Conference,
                    Teams = g.Teams.Select(t => new CFBTeamViewModel
                    {
                        Name = t.Name,
                        Logo = t.Logo,
                        Code = t.Code,
                        Conference = g.Conference,
                        EspnUrl = BuildEspnUrl(t.Code, t.Name)
                    }).ToList()
                }).ToList()
            };

            return View(viewModel);
        }

        public IActionResult Team(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !CFBGamesService.IsKnownTeamCode(code))
            {
                return RedirectToAction(nameof(Teams));
            }

            var info = CFBGamesService.GetTeamInfo(code);

            // Resolve the team's conference for display.
            var conference = CFBGamesService.GetTeamsByConference()
                .FirstOrDefault(g => g.Teams.Any(t => string.Equals(t.Code, info.Code, System.StringComparison.OrdinalIgnoreCase)))
                .Conference ?? string.Empty;

            var viewModel = new CFBTeamViewModel
            {
                Name = info.Name,
                Logo = info.Logo,
                Code = info.Code,
                Conference = conference,
                EspnUrl = BuildEspnUrl(info.Code, info.Name)
            };

            return View(viewModel);
        }
    }
}
