using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class NFLController : Controller
    {
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
    }
}
