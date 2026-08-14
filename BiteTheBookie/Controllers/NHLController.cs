using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class NHLController : Controller
    {
        private const string NewsFeedUrl = "https://www.espn.com/espn/rss/nhl/news";

        private readonly INHLScoresService _scoresService;
        private readonly INewsService _newsService;

        public NHLController(INHLScoresService scoresService, INewsService newsService)
        {
            _scoresService = scoresService;
            _newsService = newsService;
        }
        // Tuple: (Name, Division). Logo uses ESPN's lowercase code (with a few overrides).
        private static readonly Dictionary<string, (string Name, string Division)> Teams =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // Atlantic
            { "BOS", ("Boston Bruins", "Atlantic") },
            { "BUF", ("Buffalo Sabres", "Atlantic") },
            { "DET", ("Detroit Red Wings", "Atlantic") },
            { "FLA", ("Florida Panthers", "Atlantic") },
            { "MTL", ("Montreal Canadiens", "Atlantic") },
            { "OTT", ("Ottawa Senators", "Atlantic") },
            { "TBL", ("Tampa Bay Lightning", "Atlantic") },
            { "TOR", ("Toronto Maple Leafs", "Atlantic") },
            // Metropolitan
            { "CAR", ("Carolina Hurricanes", "Metropolitan") },
            { "CBJ", ("Columbus Blue Jackets", "Metropolitan") },
            { "NJD", ("New Jersey Devils", "Metropolitan") },
            { "NYI", ("New York Islanders", "Metropolitan") },
            { "NYR", ("New York Rangers", "Metropolitan") },
            { "PHI", ("Philadelphia Flyers", "Metropolitan") },
            { "PIT", ("Pittsburgh Penguins", "Metropolitan") },
            { "WSH", ("Washington Capitals", "Metropolitan") },
            // Central
            { "ARI", ("Arizona Coyotes", "Central") },
            { "CHI", ("Chicago Blackhawks", "Central") },
            { "COL", ("Colorado Avalanche", "Central") },
            { "DAL", ("Dallas Stars", "Central") },
            { "MIN", ("Minnesota Wild", "Central") },
            { "NSH", ("Nashville Predators", "Central") },
            { "STL", ("St. Louis Blues", "Central") },
            { "WPG", ("Winnipeg Jets", "Central") },
            // Pacific
            { "ANA", ("Anaheim Ducks", "Pacific") },
            { "CGY", ("Calgary Flames", "Pacific") },
            { "EDM", ("Edmonton Oilers", "Pacific") },
            { "LAK", ("Los Angeles Kings", "Pacific") },
            { "SEA", ("Seattle Kraken", "Pacific") },
            { "SJS", ("San Jose Sharks", "Pacific") },
            { "VAN", ("Vancouver Canucks", "Pacific") },
            { "VGK", ("Vegas Golden Knights", "Pacific") },
        };

        // A few teams use ESPN-specific codes that differ from our abbreviations.
        private static string EspnCode(string code) => code.ToUpperInvariant() switch
        {
            "LAK" => "la",
            "NJD" => "nj",
            "SJS" => "sj",
            "TBL" => "tb",
            _ => code.ToLowerInvariant()
        };

        private static string Logo(string code) =>
            $"https://a.espncdn.com/i/teamlogos/nhl/500/{EspnCode(code)}.png";

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var model = new LeagueHomeViewModel
            {
                LeagueName = "NHL",
                LeagueLogo = "https://a.espncdn.com/i/teamlogos/leagues/500/nhl.png",
                TeamController = "NHL",
                OddsController = "Odds",
                OddsAction = "NHL",
                ExpertPicksLeague = "NHL",
                TeamsAction = "AllTeams",
                EspnScheduleUrl = "https://www.espn.com/nhl/schedule",
                EspnStandingsUrl = "https://www.espn.com/nhl/standings"
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
                model.ErrorMessage = "Live NHL scores are unavailable right now.";
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
                LeagueName = "NHL",
                LogoUrl = "https://a.espncdn.com/i/teamlogos/leagues/500/nhl.png",
                Tagline = "Teams, matchups, odds, and expert picks",
                TeamController = "NHL",
                GameCenterAction = "NHL",
                OddsAction = "NHL",
                ExpertPicksLeague = "NHL",
                TeamsByGroup = teamsByGroup
            };

            return View("LeagueTeamGrid", vm);
        }

        public IActionResult Team(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !Teams.TryGetValue(code, out var info))
            {
                return RedirectToAction("NHL", "Picks");
            }

            var viewModel = new CFBTeamViewModel
            {
                Name = info.Name,
                Logo = Logo(code),
                Code = code.ToUpperInvariant(),
                Conference = info.Division,
                EspnUrl = string.Empty
            };

            ViewData["League"] = "NHL";
            ViewData["GameCenterController"] = "Picks";
            ViewData["GameCenterAction"] = "NHL";
            ViewData["OddsAction"] = "NHL";

            return View("LeagueTeam", viewModel);
        }
    }
}
