namespace BiteTheBookie.Models
{
    /// <summary>
    /// Live team details fetched from the ESPN Site API team endpoint. Any field may be
    /// empty when ESPN omits it for a given sport/team.
    /// </summary>
    public class EspnTeamDetails
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string Venue { get; set; } = string.Empty;
        public string VenueCity { get; set; } = string.Empty;
        public string RecordSummary { get; set; } = string.Empty;
        public string StandingSummary { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        /// <summary>True when at least one meaningful detail field was populated.</summary>
        public bool HasData =>
            !string.IsNullOrWhiteSpace(Location) ||
            !string.IsNullOrWhiteSpace(Venue) ||
            !string.IsNullOrWhiteSpace(RecordSummary) ||
            !string.IsNullOrWhiteSpace(StandingSummary);
    }
}
