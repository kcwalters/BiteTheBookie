namespace BiteTheBookie.ViewModels
{
    public class GameSimulationViewModel
    {
        public string GameId { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string League { get; set; } = "NBA";
        public string HomeTeamLogo { get; set; } = string.Empty;
        public string AwayTeamLogo { get; set; } = string.Empty;
        public string SimulationContent { get; set; } = string.Empty;
        public bool IsLoading { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>Database ID of the persisted simulation, set after save or cache hit.</summary>
        public int? SimulationId { get; set; }

        /// <summary>True when the simulation was loaded from the database rather than freshly generated.</summary>
        public bool IsFromCache { get; set; }

        /// <summary>When the simulation was originally generated (populated on cache hits).</summary>
        public DateTime? CachedAt { get; set; }
    }
}
