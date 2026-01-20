namespace BiteTheBookie.Models
{
    public class NBATickerGame
    {
        public string AwayTeam { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public int? AwayScore { get; set; }
        public int? HomeScore { get; set; }
        public string AwayLogo { get; set; } = "";
        public string HomeLogo { get; set; } = "";
        public string StatusText { get; set; } = ""; // date/time or game state
        public bool IsLive { get; set; }
        public bool IsFinal { get; set; }
        public string? EventId { get; set; }
    }
}

