namespace BiteTheBookie.Models
{   
    public class NBAGameMatchup
    {
        public string GameId { get; set; } = string.Empty;
        public string AwayTeamCode { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
        public string AwayTeamLogo { get; set; } = string.Empty;
        public string HomeTeamCode { get; set; } = string.Empty;
        public string HomeTeamName { get; set; } = string.Empty;
        public string HomeTeamLogo { get; set; } = string.Empty;
        public DateTime GameTime { get; set; }
        public string GameTimeDisplay => GameTime.ToLocalTime().ToString("MMM dd, h:mm tt");
        public decimal? Spread { get; set; }
        public decimal? OverUnder { get; set; }
        public int? AwayMoneyline { get; set; }
        public int? HomeMoneyline { get; set; }
        public int? AwayScore { get; set; }
        public int? HomeScore { get; set; }
        public string Status { get; set; } = "Scheduled";

        /// <summary>Venue/location description (e.g. "Crypto.com Arena, Los Angeles").</summary>
        public string Venue { get; set; } = string.Empty;

        /// <summary>Detailed status text from the provider (e.g. "8:00 PM ET", "Q3 4:21", "Final").</summary>
        public string StatusDetail { get; set; } = string.Empty;

        public bool IsLive => Status.Equals("Live", System.StringComparison.OrdinalIgnoreCase);
        public bool IsFinal => Status.Equals("Final", System.StringComparison.OrdinalIgnoreCase);
        public bool IsScheduled => !IsLive && !IsFinal;
    }
}

