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
        public string Status { get; set; } = "Scheduled"; // Scheduled, Live, Final

        public bool IsGameInLocalTimeZone(DateTime localDateTime, string filterLogic)
        {
            var gameDateTimeInLocalZone = TimeZoneInfo.ConvertTimeFromUtc(GameTime, TimeZoneInfo.Local);
            var isGameOnLocalDate = gameDateTimeInLocalZone.Date == localDateTime.Date;

            return filterLogic == "Show games on " + localDateTime.ToString("MMM dd") + " UTC" ? isGameOnLocalDate : !isGameOnLocalDate;
        }
    }
}
