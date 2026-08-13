namespace BiteTheBookie.ViewModels;

public class LeagueLandingViewModel
{
    public string LeagueName { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;

    // Game Center quick link
    public string GameCenterController { get; set; } = "Picks";
    public string GameCenterAction { get; set; } = string.Empty;

    // Odds quick link
    public string OddsController { get; set; } = "Odds";
    public string OddsAction { get; set; } = string.Empty;

    // Expert Picks quick link (league route value)
    public string ExpertPicksLeague { get; set; } = string.Empty;
}
