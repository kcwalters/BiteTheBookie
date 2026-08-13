using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class NBAController : Controller
    {
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

        public IActionResult Index()
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
