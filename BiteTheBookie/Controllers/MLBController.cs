using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class MLBController : Controller
    {
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
