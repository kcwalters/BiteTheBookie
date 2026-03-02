namespace BiteTheBookie.ViewModels
{
    public class GameSimulationViewModel
    {
        public string GameId { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string League { get; set; } = "NBA";
        public string HomeTeamLogo { get; set; } = string.Empty;
        public string AwayTeamLogo { get; set; } = string.Empty;
        public string SimulationContent { get; set; } = string.Empty;
        public bool IsLoading { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
