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

        /// <summary>
        /// Time zone used to anchor a game to a single calendar day. US leagues schedule
        /// by US Eastern, so a late kickoff that ends after midnight still belongs to the
        /// date it started on. Uses the IANA id, which resolves on Windows and Linux alike.
        /// </summary>
        public static readonly System.TimeZoneInfo LeagueTimeZone =
            System.TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        /// <summary>Today's date in the league time zone.</summary>
        public static DateTime LeagueToday =>
            System.TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.UtcNow, LeagueTimeZone).Date;

        /// <summary>
        /// The calendar date the game commences, in the league (US Eastern) time zone.
        /// Independent of the server time zone, so grouping/sorting by day is stable.
        /// </summary>
        public DateTime GameDate =>
            System.TimeZoneInfo.ConvertTimeFromUtc(
                GameTime.Kind == System.DateTimeKind.Utc ? GameTime : GameTime.ToUniversalTime(),
                LeagueTimeZone).Date;
        public decimal? Spread { get; set; }
        public decimal? OverUnder { get; set; }
        public int? AwayMoneyline { get; set; }
        public int? HomeMoneyline { get; set; }
        public int? AwayScore { get; set; }
        public int? HomeScore { get; set; }
        public string Status { get; set; } = "Scheduled";

        /// <summary>NFL week number (regular season), when applicable. 0 if not set.</summary>
        public int Week { get; set; }

        /// <summary>Venue/location description (e.g. "Crypto.com Arena, Los Angeles").</summary>
        public string Venue { get; set; } = string.Empty;

        /// <summary>Detailed status text from the provider (e.g. "8:00 PM ET", "Q3 4:21", "Final").</summary>
        public string StatusDetail { get; set; } = string.Empty;

        public bool IsLive => Status.Equals("Live", System.StringComparison.OrdinalIgnoreCase);
        public bool IsFinal => Status.Equals("Final", System.StringComparison.OrdinalIgnoreCase);
        public bool IsScheduled => !IsLive && !IsFinal;
    }
}

