using System.Linq;
using BiteTheBookie.Controllers;
using BiteTheBookie.Services.Implementations;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.ViewComponents
{
    /// <summary>
    /// Renders a uniform league navigation dropdown (mega-menu) with team logos and
    /// internal team-page links for any supported league.
    /// </summary>
    public class LeagueMenuViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(string league)
        {
            var key = (league ?? string.Empty).ToLowerInvariant();

            var model = key switch
            {
                "nfl" => new LeagueMenuViewModel
                {
                    LeagueKey = "nfl",
                    DisplayName = "NFL",
                    Controller = "NFL",
                    LeagueIcon = "https://a.espncdn.com/i/teamlogos/leagues/500/nfl.png",
                    Groups = NFLController.GetMenuGroups()
                },
                "mlb" => new LeagueMenuViewModel
                {
                    LeagueKey = "mlb",
                    DisplayName = "MLB",
                    Controller = "MLB",
                    LeagueIcon = "https://a.espncdn.com/i/teamlogos/leagues/500/mlb.png",
                    Groups = MLBController.GetMenuGroups()
                },
                "nba" => new LeagueMenuViewModel
                {
                    LeagueKey = "nba",
                    DisplayName = "NBA",
                    Controller = "NBA",
                    LeagueIcon = "https://a.espncdn.com/i/teamlogos/leagues/500/nba.png",
                    Groups = NBAController.GetMenuGroups()
                },
                "nhl" => new LeagueMenuViewModel
                {
                    LeagueKey = "nhl",
                    DisplayName = "NHL",
                    Controller = "NHL",
                    LeagueIcon = "https://a.espncdn.com/i/teamlogos/leagues/500/nhl.png",
                    Groups = NHLController.GetMenuGroups()
                },
                "cbb" => new LeagueMenuViewModel
                {
                    LeagueKey = "ncaa",
                    DisplayName = "CBB",
                    Controller = "CollegeBasketball",
                    LeagueIcon = "/img/NCAAMens_med.png",
                    Groups = CollegeBasketballController.GetMenuGroups()
                },
                "cfb" => new LeagueMenuViewModel
                {
                    LeagueKey = "ncaaf",
                    DisplayName = "CFB",
                    Controller = "CollegeFootball",
                    LeagueIcon = "/img/NCAAMens_med.png",
                    Groups = CFBGamesService.GetTeamsByConference()
                        .Select(g => new LeagueMenuGroup
                        {
                            Title = g.Conference,
                            Teams = g.Teams.Select(t => new LeagueMenuTeam
                            {
                                Name = t.Name,
                                Code = t.Code,
                                Logo = t.Logo
                            }).ToList()
                        }).ToList()
                },
                _ => new LeagueMenuViewModel { LeagueKey = key, DisplayName = key.ToUpperInvariant() }
            };

            return View(model);
        }
    }
}
