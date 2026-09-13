namespace BiteTheBookie.ViewModels
{
    public class CFBTeamViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string EspnUrl { get; set; } = string.Empty;
        public string Conference { get; set; } = string.Empty;

        // Live team details (from ESPN); any may be empty.
        public string Location { get; set; } = string.Empty;
        public string Venue { get; set; } = string.Empty;
        public string VenueCity { get; set; } = string.Empty;
        public string RecordSummary { get; set; } = string.Empty;
        public string StandingSummary { get; set; } = string.Empty;

        // Curated description of the team's conference/division.
        public string ConferenceDescription { get; set; } = string.Empty;

        // Recent news about this team (paragraph 1) and its league (paragraph 2).
        public List<string> TeamNews { get; set; } = new();
        public List<string> LeagueNews { get; set; } = new();

        // Current-season schedule and results (from ESPN); shown in the right column.
        public List<BiteTheBookie.Models.EspnScheduleEntry> Schedule { get; set; } = new();

        /// <summary>True when at least one live team-info field is available to display.</summary>
        public bool HasTeamInfo =>
            !string.IsNullOrWhiteSpace(Location) ||
            !string.IsNullOrWhiteSpace(Venue) ||
            !string.IsNullOrWhiteSpace(RecordSummary) ||
            !string.IsNullOrWhiteSpace(StandingSummary);
    }

    public class CFBConferenceViewModel
    {
        public string Conference { get; set; } = string.Empty;
        public List<CFBTeamViewModel> Teams { get; set; } = new List<CFBTeamViewModel>();
    }

    public class CFBTeamsViewModel
    {
        public List<CFBConferenceViewModel> Conferences { get; set; } = new List<CFBConferenceViewModel>();
    }
}
