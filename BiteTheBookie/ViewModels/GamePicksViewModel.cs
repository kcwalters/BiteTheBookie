using BiteTheBookie.Helpers;

namespace BiteTheBookie.ViewModels
{
    public class GamePicksViewModel
    {
        public string GameId { get; set; } = string.Empty;
        public string League { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
        public string HomeTeamName { get; set; } = string.Empty;
        public string AwayTeamLogo { get; set; } = string.Empty;
        public string HomeTeamLogo { get; set; } = string.Empty;
        public DateTime GameTime { get; set; }
        public string VenueTimeZoneId { get; set; } = "Eastern Standard Time";
        public string GameTimeDisplay =>
            VenueTimeZoneHelper.FormatVenueTime(GameTime, VenueTimeZoneId);
        public decimal? Spread { get; set; }
        public decimal? OverUnder { get; set; }
        public int? HomeMoneyline { get; set; }
        public int? AwayMoneyline { get; set; }
        public List<PickDetail> Picks { get; set; } = new();
    }

    public class PickDetail
    {
        public string PickType { get; set; } = string.Empty;
        public string PickSelection { get; set; } = string.Empty;
        public int Confidence { get; set; }
        public string? Analysis { get; set; }
        public string EnteredBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}