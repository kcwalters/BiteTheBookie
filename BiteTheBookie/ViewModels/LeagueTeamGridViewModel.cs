namespace BiteTheBookie.ViewModels;

public class LeagueTeamGridViewModel
{
    public string LeagueName { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;

    // Controller that hosts the Team detail action (e.g., "NBA", "CollegeBasketball")
    public string TeamController { get; set; } = string.Empty;

    // Quick links
    public string GameCenterController { get; set; } = "Picks";
    public string GameCenterAction { get; set; } = string.Empty;
    public string OddsController { get; set; } = "Odds";
    public string OddsAction { get; set; } = string.Empty;
    public string ExpertPicksLeague { get; set; } = string.Empty;

    // Teams grouped by division/conference
    public List<IGrouping<string, NFLTeamListItem>> TeamsByGroup { get; set; } = new();
}
