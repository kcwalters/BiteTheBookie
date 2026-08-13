using BiteTheBookie.Services.Implementations;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class CollegeFootballController : Controller
    {
        public IActionResult Index()
        {
            var teamsByGroup = CFBGamesService.GetTeamsByConference()
                .SelectMany(g => g.Teams.Select(t => new NFLTeamListItem
                {
                    Code = t.Code,
                    Name = t.Name,
                    Division = g.Conference,
                    Logo = t.Logo
                }))
                .OrderBy(t => t.Division)
                .ThenBy(t => t.Name)
                .GroupBy(t => t.Division)
                .ToList();

            var vm = new LeagueTeamGridViewModel
            {
                LeagueName = "CFB",
                LogoUrl = "/img/NCAAMens_med.png",
                Tagline = "Teams, matchups, odds, and expert picks",
                TeamController = "CollegeFootball",
                GameCenterAction = "CFB",
                OddsAction = "CFB",
                ExpertPicksLeague = "CFB",
                TeamsByGroup = teamsByGroup
            };

            return View("LeagueTeamGrid", vm);
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
