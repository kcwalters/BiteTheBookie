namespace BiteTheBookie.Models
{
    public class NBATeamRoster
    {
        public string TeamCode { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public List<NBAPlayer> Players { get; set; } = new();
    }

    public class NBAPlayer
    {
        public string Name         { get; set; } = string.Empty;
        public string Position     { get; set; } = string.Empty;
        public bool   IsStarter    { get; set; }

        /// <summary>Current-season averages from ESPN roster endpoint (0 if unavailable).</summary>
        public double PointsPerGame   { get; set; }
        public double ReboundsPerGame { get; set; }
        public double AssistsPerGame  { get; set; }
    }
}
