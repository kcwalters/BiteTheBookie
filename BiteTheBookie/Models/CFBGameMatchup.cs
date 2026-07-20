namespace BiteTheBookie.Models
{
    public class CFBGameMatchup
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
    }
}
