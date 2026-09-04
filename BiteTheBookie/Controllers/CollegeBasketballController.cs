using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class CollegeBasketballController : Controller
    {
        private const string NewsFeedUrl = "https://www.espn.com/espn/rss/ncb/news";

        private readonly INCAAScoresService _scoresService;
        private readonly INewsService _newsService;
        private readonly ILeagueScheduleService _scheduleService;

        public CollegeBasketballController(INCAAScoresService scoresService, INewsService newsService, ILeagueScheduleService scheduleService)
        {
            _scoresService = scoresService;
            _newsService = newsService;
            _scheduleService = scheduleService;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var model = new LeagueHomeViewModel
            {
                LeagueName = "CBB",
                LeagueLogo = "/img/NCAAMens_med.png",
                TeamController = "CollegeBasketball",
                OddsController = "Odds",
                OddsAction = "CBB",
                ExpertPicksLeague = "CBB",
                TeamsAction = "AllTeams",
                EspnScheduleUrl = "https://www.espn.com/mens-college-basketball/schedule",
                EspnStandingsUrl = "https://www.espn.com/mens-college-basketball/standings"
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
                model.ErrorMessage = "Live CBB scores are unavailable right now.";
            }

            try
            {
                model.UpcomingGames = await GetUpcomingGamesAsync("CBB", cancellationToken);
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
                    Division = t.Value.Conference,
                    Logo = $"https://a.espncdn.com/i/teamlogos/ncaa/500/{t.Value.EspnId}.png"
                })
                .OrderBy(t => t.Division)
                .ThenBy(t => t.Name)
                .GroupBy(t => t.Division)
                .ToList();

            var vm = new LeagueTeamGridViewModel
            {
                LeagueName = "CBB",
                LogoUrl = "/img/NCAAMens_med.png",
                Tagline = "Teams, matchups, odds, and expert picks",
                TeamController = "CollegeBasketball",
                GameCenterAction = "CBB",
                OddsAction = "CBB",
                ExpertPicksLeague = "CBB",
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
                    division = t.Value.Conference,
                    logo = $"https://a.espncdn.com/i/teamlogos/ncaa/500/{t.Value.EspnId}.png"
                })
                .OrderBy(t => t.division)
                .ThenBy(t => t.name)
                .ToList();

            return Json(teams);
        }

        // Codes MUST match wwwroot/js/nfl-team-modal.js cbbTeams / cbbColumns.
        // Tuple: (Name, Conference, EspnId) — EspnId is the shared ESPN school id
        // used for both the logo (ncaa/500/{id}.png) and the team deep-link.
        private static readonly Dictionary<string, (string Name, string Conference, string EspnId)> Teams =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // ACC
            { "DUKE", ("Duke", "ACC", "150") },
            { "UNC",  ("North Carolina", "ACC", "153") },
            { "UVA",  ("Virginia", "ACC", "258") },
            { "CLEM", ("Clemson", "ACC", "228") },
            { "NCSU", ("NC State", "ACC", "152") },
            { "WAKE", ("Wake Forest", "ACC", "154") },
            { "VT",   ("Virginia Tech", "ACC", "259") },
            { "MIA",  ("Miami", "ACC", "2390") },
            { "FSU",  ("Florida State", "ACC", "52") },
            { "LOU",  ("Louisville", "ACC", "97") },
            { "PITT", ("Pittsburgh", "ACC", "221") },
            { "SYR",  ("Syracuse", "ACC", "183") },
            { "BC",   ("Boston College", "ACC", "103") },
            { "GT",   ("Georgia Tech", "ACC", "59") },
            { "ND",   ("Notre Dame", "ACC", "87") },
            // Big Ten
            { "ILL",  ("Illinois", "Big Ten", "356") },
            { "IND",  ("Indiana", "Big Ten", "84") },
            { "IOWA", ("Iowa", "Big Ten", "2294") },
            { "MD",   ("Maryland", "Big Ten", "120") },
            { "MICH", ("Michigan", "Big Ten", "130") },
            { "MSU",  ("Michigan State", "Big Ten", "127") },
            { "MINN", ("Minnesota", "Big Ten", "135") },
            { "NEB",  ("Nebraska", "Big Ten", "158") },
            { "NW",   ("Northwestern", "Big Ten", "77") },
            { "OSU",  ("Ohio State", "Big Ten", "194") },
            { "PSU",  ("Penn State", "Big Ten", "213") },
            { "PUR",  ("Purdue", "Big Ten", "2509") },
            { "RUT",  ("Rutgers", "Big Ten", "164") },
            { "WIS",  ("Wisconsin", "Big Ten", "275") },
            // Big 12
            { "BAY",  ("Baylor", "Big 12", "239") },
            { "ISU",  ("Iowa State", "Big 12", "66") },
            { "KU",   ("Kansas", "Big 12", "2305") },
            { "KSU",  ("Kansas State", "Big 12", "2306") },
            { "OU",   ("Oklahoma", "Big 12", "201") },
            { "OST",  ("Oklahoma State", "Big 12", "197") },
            { "TCU",  ("TCU", "Big 12", "2628") },
            { "TEX",  ("Texas", "Big 12", "251") },
            { "TTU",  ("Texas Tech", "Big 12", "2641") },
            { "WVU",  ("West Virginia", "Big 12", "277") },
            // SEC
            { "ALA",  ("Alabama", "SEC", "333") },
            { "ARK",  ("Arkansas", "SEC", "8") },
            { "AUB",  ("Auburn", "SEC", "2") },
            { "FLA",  ("Florida", "SEC", "57") },
            { "UGA",  ("Georgia", "SEC", "61") },
            { "UK",   ("Kentucky", "SEC", "96") },
            { "LSU",  ("LSU", "SEC", "99") },
            { "MISS", ("Ole Miss", "SEC", "145") },
            { "MST",  ("Mississippi State", "SEC", "344") },
            { "USC",  ("South Carolina", "SEC", "2579") },
            { "TENN", ("Tennessee", "SEC", "2633") },
            { "TAMU", ("Texas A&M", "SEC", "245") },
            { "VAN",  ("Vanderbilt", "SEC", "238") },
            // Pac-12
            { "ARIZ", ("Arizona", "Pac-12", "12") },
            { "ASU",  ("Arizona State", "Pac-12", "9") },
            { "CAL",  ("California", "Pac-12", "25") },
            { "COLO", ("Colorado", "Pac-12", "38") },
            { "ORE",  ("Oregon", "Pac-12", "2483") },
            { "ORST", ("Oregon State", "Pac-12", "204") },
            { "STAN", ("Stanford", "Pac-12", "24") },
            { "UCLA", ("UCLA", "Pac-12", "26") },
            { "WASH", ("Washington", "Pac-12", "264") },
            { "WSU",  ("Washington State", "Pac-12", "265") },
            // Big East
            { "BUT",  ("Butler", "Big East", "2086") },
            { "CRE",  ("Creighton", "Big East", "156") },
            { "DPU",  ("DePaul", "Big East", "305") },
            { "GTWN", ("Georgetown", "Big East", "46") },
            { "MARQ", ("Marquette", "Big East", "269") },
            { "PROV", ("Providence", "Big East", "2507") },
            { "SHU",  ("Seton Hall", "Big East", "2550") },
            { "SJU",  ("St. John's", "Big East", "2599") },
            { "VILL", ("Villanova", "Big East", "222") },
            { "XAV",  ("Xavier", "Big East", "2752") },
        };

        public IActionResult Team(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !Teams.TryGetValue(code, out var info))
            {
                return RedirectToAction("CBB", "Picks");
            }

            var viewModel = new CFBTeamViewModel
            {
                Name = info.Name,
                Logo = $"https://a.espncdn.com/i/teamlogos/ncaa/500/{info.EspnId}.png",
                Code = code.ToUpperInvariant(),
                Conference = info.Conference,
                EspnUrl = BuildEspnUrl(info.EspnId, info.Name)
            };

            return View(viewModel);
        }

        private static string BuildEspnUrl(string espnId, string name)
        {
            if (string.IsNullOrEmpty(espnId))
                return "https://www.espn.com/mens-college-basketball/teams";

            var slug = name.ToLowerInvariant()
                .Replace("'", string.Empty)
                .Replace("&", "and")
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", "-");

            return $"https://www.espn.com/mens-college-basketball/team/_/id/{espnId}/{slug}";
        }
    }
}
