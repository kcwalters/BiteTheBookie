using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class MLBController : Controller
    {
        private const string NewsFeedUrl = "https://www.espn.com/espn/rss/mlb/news";

        private readonly IMLBGamesService _gamesService;
        private readonly INewsService _newsService;

        public MLBController(IMLBGamesService gamesService, INewsService newsService)
        {
            _gamesService = gamesService;
            _newsService = newsService;
        }

        // Tuple: (Name, Division). Logo uses ESPN's lowercase abbreviation code.
        private static readonly Dictionary<string, (string Name, string Division)> Teams =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // AL East
            { "BAL", ("Baltimore Orioles", "AL East") },
            { "BOS", ("Boston Red Sox", "AL East") },
            { "NYY", ("New York Yankees", "AL East") },
            { "TB", ("Tampa Bay Rays", "AL East") },
            { "TOR", ("Toronto Blue Jays", "AL East") },
            // AL Central
            { "CWS", ("Chicago White Sox", "AL Central") },
            { "CLE", ("Cleveland Guardians", "AL Central") },
            { "DET", ("Detroit Tigers", "AL Central") },
            { "KC", ("Kansas City Royals", "AL Central") },
            { "MIN", ("Minnesota Twins", "AL Central") },
            // AL West
            { "HOU", ("Houston Astros", "AL West") },
            { "LAA", ("Los Angeles Angels", "AL West") },
            { "OAK", ("Oakland Athletics", "AL West") },
            { "SEA", ("Seattle Mariners", "AL West") },
            { "TEX", ("Texas Rangers", "AL West") },
            // NL East
            { "ATL", ("Atlanta Braves", "NL East") },
            { "MIA", ("Miami Marlins", "NL East") },
            { "NYM", ("New York Mets", "NL East") },
            { "PHI", ("Philadelphia Phillies", "NL East") },
            { "WSH", ("Washington Nationals", "NL East") },
            // NL Central
            { "CHC", ("Chicago Cubs", "NL Central") },
            { "CIN", ("Cincinnati Reds", "NL Central") },
            { "MIL", ("Milwaukee Brewers", "NL Central") },
            { "PIT", ("Pittsburgh Pirates", "NL Central") },
            { "STL", ("St. Louis Cardinals", "NL Central") },
            // NL West
            { "ARI", ("Arizona Diamondbacks", "NL West") },
            { "COL", ("Colorado Rockies", "NL West") },
            { "LAD", ("Los Angeles Dodgers", "NL West") },
            { "SD", ("San Diego Padres", "NL West") },
            { "SF", ("San Francisco Giants", "NL West") },
        };

        private static string Logo(string code) =>
            $"https://a.espncdn.com/i/teamlogos/mlb/500/{code.ToLowerInvariant()}.png";

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var model = new LeagueHomeViewModel
            {
                LeagueName = "MLB",
                LeagueLogo = "https://a.espncdn.com/i/teamlogos/leagues/500/mlb.png",
                TeamController = "MLB",
                OddsController = "Odds",
                OddsAction = "MLB",
                ExpertPicksLeague = "MLB",
                TeamsAction = "AllTeams",
                EspnScheduleUrl = "https://www.espn.com/mlb/schedule",
                EspnStandingsUrl = "https://www.espn.com/mlb/standings"
            };

            try
            {
                var games = await _gamesService.GetTodayGamesAsync();
                model.Games = games.Select(g => new NBAGameMatchup
                {
                    AwayTeamName = g.AwayTeam,
                    AwayTeamLogo = g.AwayTeamLogoUrl ?? string.Empty,
                    AwayScore = g.AwayScore,
                    HomeTeamName = g.HomeTeam,
                    HomeTeamLogo = g.HomeTeamLogoUrl ?? string.Empty,
                    HomeScore = g.HomeScore,
                    GameTime = g.GameTime ?? DateTime.MinValue,
                    StatusDetail = g.Status,
                    Status = MapStatus(g.Status)
                }).ToList();
            }
            catch
            {
                model.ErrorMessage = "Live MLB scores are unavailable right now.";
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

        // Normalizes the MLB provider status string into the shared Scheduled/Live/Final states.
        private static string MapStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "Scheduled";

            if (status.Contains("Final", StringComparison.OrdinalIgnoreCase))
                return "Final";

            if (status.Contains("In Progress", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Live", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Top", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Bottom", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Mid", StringComparison.OrdinalIgnoreCase))
                return "Live";

            return "Scheduled";
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
                LeagueName = "MLB",
                LogoUrl = "https://a.espncdn.com/i/teamlogos/leagues/500/mlb.png",
                Tagline = "Teams, matchups, odds, and expert picks",
                TeamController = "MLB",
                GameCenterAction = "MLB",
                OddsAction = "MLB",
                ExpertPicksLeague = "MLB",
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
                return RedirectToAction("MLB", "Picks");
            }

            var viewModel = new CFBTeamViewModel
            {
                Name = info.Name,
                Logo = Logo(code),
                Code = code.ToUpperInvariant(),
                Conference = info.Division,
                EspnUrl = string.Empty
            };

            ViewData["League"] = "MLB";
            ViewData["GameCenterController"] = "Picks";
            ViewData["GameCenterAction"] = "MLB";
            ViewData["OddsAction"] = "MLB";

            return View("LeagueTeam", viewModel);
        }
    }
}
