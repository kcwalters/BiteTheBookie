using BiteTheBookie.ViewModels;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class NFLController : Controller
    {
        private const string NflNewsFeedUrl = "https://www.espn.com/espn/rss/nfl/news";

        private readonly INFLScoresService _scoresService;
        private readonly INewsService _newsService;
        private readonly ILeagueScheduleService _scheduleService;

        public NFLController(INFLScoresService scoresService, INewsService newsService, ILeagueScheduleService scheduleService)
        {
            _scoresService = scoresService;
            _newsService = newsService;
            _scheduleService = scheduleService;
        }

        // Codes MUST match wwwroot/js/nfl-team-modal.js nflTeams / nflColumns.
        // Tuple: (Name, Division) — logo/deep-link use the ESPN lowercase code.
        private static readonly Dictionary<string, (string Name, string Division)> Teams =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // AFC East
            { "BUF", ("Buffalo Bills", "AFC East") },
            { "MIA", ("Miami Dolphins", "AFC East") },
            { "NE",  ("New England Patriots", "AFC East") },
            { "NYJ", ("New York Jets", "AFC East") },
            // AFC North
            { "BAL", ("Baltimore Ravens", "AFC North") },
            { "CIN", ("Cincinnati Bengals", "AFC North") },
            { "CLE", ("Cleveland Browns", "AFC North") },
            { "PIT", ("Pittsburgh Steelers", "AFC North") },
            // AFC South
            { "HOU", ("Houston Texans", "AFC South") },
            { "IND", ("Indianapolis Colts", "AFC South") },
            { "JAX", ("Jacksonville Jaguars", "AFC South") },
            { "TEN", ("Tennessee Titans", "AFC South") },
            // AFC West
            { "DEN", ("Denver Broncos", "AFC West") },
            { "KC",  ("Kansas City Chiefs", "AFC West") },
            { "LAC", ("Los Angeles Chargers", "AFC West") },
            { "LV",  ("Las Vegas Raiders", "AFC West") },
            // NFC East
            { "DAL", ("Dallas Cowboys", "NFC East") },
            { "NYG", ("New York Giants", "NFC East") },
            { "PHI", ("Philadelphia Eagles", "NFC East") },
            { "WAS", ("Washington Commanders", "NFC East") },
            // NFC North
            { "CHI", ("Chicago Bears", "NFC North") },
            { "DET", ("Detroit Lions", "NFC North") },
            { "GB",  ("Green Bay Packers", "NFC North") },
            { "MIN", ("Minnesota Vikings", "NFC North") },
            // NFC South
            { "ATL", ("Atlanta Falcons", "NFC South") },
            { "CAR", ("Carolina Panthers", "NFC South") },
            { "NO",  ("New Orleans Saints", "NFC South") },
            { "TB",  ("Tampa Bay Buccaneers", "NFC South") },
            // NFC West
            { "ARI", ("Arizona Cardinals", "NFC West") },
            { "LAR", ("Los Angeles Rams", "NFC West") },
            { "SEA", ("Seattle Seahawks", "NFC West") },
            { "SF",  ("San Francisco 49ers", "NFC West") },
        };

        public IActionResult Team(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !Teams.TryGetValue(code, out var info))
            {
                return RedirectToAction("NFL", "Picks");
            }

            var espnCode = EspnCode(code);

            var viewModel = new CFBTeamViewModel
            {
                Name = info.Name,
                Logo = $"https://a.espncdn.com/i/teamlogos/nfl/500/{espnCode}.png",
                Code = code.ToUpperInvariant(),
                Conference = info.Division,
                EspnUrl = BuildEspnUrl(espnCode, info.Name)
            };

            return View(viewModel);
        }

        // Washington uses ESPN code "wsh"; all others match the lowercase abbreviation.
        private static string EspnCode(string code) =>
            string.Equals(code, "WAS", StringComparison.OrdinalIgnoreCase) ? "wsh" : code.ToLowerInvariant();

        private static string BuildEspnUrl(string espnCode, string name)
        {
            var slug = name.ToLowerInvariant()
                .Replace("'", string.Empty)
                .Replace("&", "and")
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", "-");

            return $"https://www.espn.com/nfl/team/_/name/{espnCode}/{slug}";
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var model = new NFLLandingViewModel();

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
                model.ErrorMessage = "Live NFL scores are unavailable right now.";
            }

            try
            {
                model.UpcomingGames = await GetUpcomingGamesAsync("NFL", cancellationToken);
            }
            catch
            {
                // Upcoming games are optional; the view handles an empty list.
            }

            try
            {
                model.Headlines = (await _newsService.GetLatestNewsAsync(NflNewsFeedUrl, 9)).ToList();
            }
            catch
            {
                // Headlines are optional; the view handles an empty list.
            }

            return View(model);
        }

        // Fetches all games for the current week (Monday through Sunday) from the live
        // ESPN schedule feed.
        private async Task<List<NBAGameMatchup>> GetUpcomingGamesAsync(string league, CancellationToken cancellationToken)
        {
            var upcoming = new List<NBAGameMatchup>();

            // Find Monday of the current week (Monday-Sunday span).
            var today = DateTime.Today;
            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var monday = today.AddDays(-daysSinceMonday);

            for (var i = 0; i < 7; i++)
            {
                var dayGames = await _scheduleService.GetGamesForDateAsync(league, monday.AddDays(i), cancellationToken);
                upcoming.AddRange(dayGames);
            }

            return upcoming;
        }

        public IActionResult AllTeams()
        {
            var teamsByDivision = Teams
                .Select(t => new NFLTeamListItem
                {
                    Code = t.Key.ToUpperInvariant(),
                    Name = t.Value.Name,
                    Division = t.Value.Division,
                    Logo = $"https://a.espncdn.com/i/teamlogos/nfl/500/{EspnCode(t.Key)}.png"
                })
                .OrderBy(t => t.Division)
                .ThenBy(t => t.Name)
                .GroupBy(t => t.Division)
                .ToList();

            return View(teamsByDivision);
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
                    logo = $"https://a.espncdn.com/i/teamlogos/nfl/500/{EspnCode(t.Key)}.png"
                })
                .OrderBy(t => t.division)
                .ThenBy(t => t.name)
                .ToList();

            return Json(teams);
        }
    }
}
