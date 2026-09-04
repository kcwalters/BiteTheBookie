using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class NBAController : Controller
    {
        private const string NewsFeedUrl = "https://www.espn.com/espn/rss/nba/news";

        private readonly INBAScoresService _scoresService;
        private readonly INewsService _newsService;
        private readonly ILeagueScheduleService _scheduleService;

        public NBAController(INBAScoresService scoresService, INewsService newsService, ILeagueScheduleService scheduleService)
        {
            _scoresService = scoresService;
            _newsService = newsService;
            _scheduleService = scheduleService;
        }

        // Tuple: (Name, Division). Logo uses ESPN's lowercase abbreviation code.
        private static readonly Dictionary<string, (string Name, string Division)> Teams =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // Atlantic
            { "BOS", ("Boston Celtics", "Atlantic") },
            { "BKN", ("Brooklyn Nets", "Atlantic") },
            { "NYK", ("New York Knicks", "Atlantic") },
            { "PHI", ("Philadelphia 76ers", "Atlantic") },
            { "TOR", ("Toronto Raptors", "Atlantic") },
            // Central
            { "CHI", ("Chicago Bulls", "Central") },
            { "CLE", ("Cleveland Cavaliers", "Central") },
            { "DET", ("Detroit Pistons", "Central") },
            { "IND", ("Indiana Pacers", "Central") },
            { "MIL", ("Milwaukee Bucks", "Central") },
            // Southeast
            { "ATL", ("Atlanta Hawks", "Southeast") },
            { "CHA", ("Charlotte Hornets", "Southeast") },
            { "MIA", ("Miami Heat", "Southeast") },
            { "ORL", ("Orlando Magic", "Southeast") },
            { "WAS", ("Washington Wizards", "Southeast") },
            // Northwest
            { "DEN", ("Denver Nuggets", "Northwest") },
            { "MIN", ("Minnesota Timberwolves", "Northwest") },
            { "OKC", ("Oklahoma City Thunder", "Northwest") },
            { "POR", ("Portland Trail Blazers", "Northwest") },
            { "UTA", ("Utah Jazz", "Northwest") },
            // Pacific
            { "GSW", ("Golden State Warriors", "Pacific") },
            { "LAC", ("LA Clippers", "Pacific") },
            { "LAL", ("Los Angeles Lakers", "Pacific") },
            { "PHX", ("Phoenix Suns", "Pacific") },
            { "SAC", ("Sacramento Kings", "Pacific") },
            // Southwest
            { "DAL", ("Dallas Mavericks", "Southwest") },
            { "HOU", ("Houston Rockets", "Southwest") },
            { "MEM", ("Memphis Grizzlies", "Southwest") },
            { "NOP", ("New Orleans Pelicans", "Southwest") },
            { "SAS", ("San Antonio Spurs", "Southwest") },
        };

        private static string Logo(string code) =>
            $"https://a.espncdn.com/i/teamlogos/nba/500/{code.ToLowerInvariant()}.png";

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var model = new LeagueHomeViewModel
            {
                LeagueName = "NBA",
                LeagueLogo = "https://a.espncdn.com/i/teamlogos/leagues/500/nba.png",
                TeamController = "NBA",
                OddsController = "Odds",
                OddsAction = "NBA",
                ExpertPicksLeague = "NBA",
                TeamsAction = "AllTeams",
                EspnScheduleUrl = "https://www.espn.com/nba/schedule",
                EspnStandingsUrl = "https://www.espn.com/nba/standings"
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
                model.ErrorMessage = "Live NBA scores are unavailable right now.";
            }

            try
            {
                model.UpcomingGames = await GetUpcomingGamesAsync("NBA", cancellationToken);
            }
            catch
            {
                // Upcoming games are optional; the view handles an empty list.
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

        // Fetches upcoming (non-final) games for the current week from the live ESPN
        // schedule feed: today through the next six days.
        private async Task<List<NBAGameMatchup>> GetUpcomingGamesAsync(string league, CancellationToken cancellationToken)
        {
            var upcoming = new List<NBAGameMatchup>();
            var today = DateTime.Today;
            for (var i = 0; i < 7; i++)
            {
                var dayGames = await _scheduleService.GetGamesForDateAsync(league, today.AddDays(i), cancellationToken);
                upcoming.AddRange(dayGames.Where(g => !g.IsFinal));
            }

            return upcoming;
        }

        public IActionResult AllTeams()
        {
            var teamsByGroup = Teams
                .Select(t => new NFLTeamListItem
                {
                    Code = t.Key.ToUpperInvariant(),
                    Name = t.Value.Name,
                    Division = t.Value.Division,
                    Logo = Logo(t.Key)
                })
                .OrderBy(t => t.Division)
                .ThenBy(t => t.Name)
                .GroupBy(t => t.Division)
                .ToList();

            var vm = new LeagueTeamGridViewModel
            {
                LeagueName = "NBA",
                LogoUrl = "https://a.espncdn.com/i/teamlogos/leagues/500/nba.png",
                Tagline = "Teams, matchups, odds, and expert picks",
                TeamController = "NBA",
                GameCenterAction = "NBA",
                OddsAction = "NBA",
                ExpertPicksLeague = "NBA",
                TeamsByGroup = teamsByGroup
            };

            return View("LeagueTeamGrid", vm);
        }

        // Lightweight JSON feed used by the nav hover dropdown.
        [ResponseCache(Duration = 3600)]
        public IActionResult NavTeams()
        {
            var teams = Teams
                .Select(t => new
                {
                    code = t.Key.ToUpperInvariant(),
                    name = t.Value.Name,
                    division = t.Value.Division,
                    logo = Logo(t.Key)
                })
                .OrderBy(t => t.division)
                .ThenBy(t => t.name)
                .ToList();

            return Json(teams);
        }

        public IActionResult Team(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !Teams.TryGetValue(code, out var info))
            {
                return RedirectToAction("NBA", "Picks");
            }

            var viewModel = new CFBTeamViewModel
            {
                Name = info.Name,
                Logo = Logo(code),
                Code = code.ToUpperInvariant(),
                Conference = info.Division,
                EspnUrl = string.Empty
            };

            ViewData["League"] = "NBA";
            ViewData["GameCenterController"] = "Picks";
            ViewData["GameCenterAction"] = "NBA";
            ViewData["OddsAction"] = "NBA";

            return View("LeagueTeam", viewModel);
        }
    }
}
