namespace BiteTheBookie.Models
{
    /// <summary>
    /// Records that a user unlocked the premium expert picks for a specific game.
    /// Used to enforce the weekly game-unlock limit for the Pro tier
    /// (one unlocked game counts as one of the weekly picks).
    /// </summary>
    public class ExpertPickView
    {
        public int Id { get; set; }

        /// <summary>The Identity user id that unlocked the game.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>The external game identifier that was unlocked.</summary>
        public string GameId { get; set; } = string.Empty;

        /// <summary>The league the unlocked game belongs to.</summary>
        public string League { get; set; } = string.Empty;

        /// <summary>When the game was unlocked (UTC).</summary>
        public DateTime ViewedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Start of the (Monday-based, UTC) week in which the game was unlocked.</summary>
        public DateTime WeekStartUtc { get; set; }
    }
}
