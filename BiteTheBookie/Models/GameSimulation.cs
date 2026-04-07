namespace BiteTheBookie.Models
{
    public class GameSimulation
    {
        public int Id { get; set; }

        /// <summary>Matches the GameId format used throughout the app (e.g. "bos-mil-20260406").</summary>
        public string GameId { get; set; } = string.Empty;

        public string League { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
        public string HomeTeamName { get; set; } = string.Empty;
        public DateTime GameDate { get; set; }

        /// <summary>Full AI-generated markdown content.</summary>
        public string SimulationContent { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Optional — set when the requesting user is authenticated.</summary>
        public string? GeneratedByUserId { get; set; }
    }
}