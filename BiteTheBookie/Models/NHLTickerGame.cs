namespace BiteTheBookie.Models
{

    public class NHLTickerGame
    {
        public string AwayTeam { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public int? AwayScore { get; set; }
        public int? HomeScore { get; set; }

        public string AwayLogo { get; set; } = "";
        public string HomeLogo { get; set; } = "";

        public bool AwayHasPossession { get; set; }
        public bool HomeHasPossession { get; set; }

        public bool AwayInRedZone { get; set; }
        public bool HomeInRedZone { get; set; }

        public string StatusText { get; set; } = "";
        public bool IsLive { get; set; }
        public bool IsFinal { get; set; }

        public string? EventId { get; set; }
    }
}