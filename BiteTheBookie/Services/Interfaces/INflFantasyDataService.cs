using BiteTheBookie.Models.Fantasy;

namespace BiteTheBookie.Services.Interfaces
{
    /// <summary>
    /// Builds and scores Daily Fantasy Football contests from real NFL data (ESPN API).
    /// </summary>
    public interface INflFantasyDataService
    {
        /// <summary>
        /// Returns the NFL slate (games) for the given date from the real ESPN scoreboard.
        /// </summary>
        Task<IReadOnlyList<FantasySlateGame>> GetSlateGamesAsync(DateTime date, CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds the selectable player pool (with derived salaries) for a slate date using
        /// real ESPN rosters/stats. Includes team DST entries.
        /// </summary>
        Task<IReadOnlyList<FantasyPlayer>> BuildPlayerPoolAsync(DateTime date, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches finalized real fantasy points for the players in a contest, keyed by ExternalId.
        /// Only returns values for completed games.
        /// </summary>
        Task<IReadOnlyDictionary<string, decimal>> GetActualFantasyPointsAsync(FantasyContest contest, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Lightweight description of a real NFL game on a slate.
    /// </summary>
    public sealed class FantasySlateGame
    {
        public string GameId { get; set; } = string.Empty;
        public string AwayTeamCode { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
        public string HomeTeamCode { get; set; } = string.Empty;
        public string HomeTeamName { get; set; } = string.Empty;
        public DateTime GameTime { get; set; }
        public string Status { get; set; } = "Scheduled";
    }
}
