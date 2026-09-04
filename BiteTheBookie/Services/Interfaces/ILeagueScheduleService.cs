using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    /// <summary>
    /// Fetches a schedule for any supported league on a specific calendar date directly
    /// from the ESPN scoreboard API, returning accurate teams, start times, venues, and
    /// live status.
    /// </summary>
    public interface ILeagueScheduleService
    {
        /// <summary>
        /// Returns the games scheduled for the given league and date. Games are ordered
        /// by start time and each carries its Scheduled/Live/Final status.
        /// </summary>
        /// <param name="league">League code: NBA, NFL, NHL, MLB, CFB, or CBB.</param>
        Task<List<NBAGameMatchup>> GetGamesForDateAsync(string league, DateTime date, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns every NFL regular-season game for the current season, with each game's
        /// <see cref="NBAGameMatchup.Week"/> populated. Games are ordered by week then start time.
        /// </summary>
        Task<List<NBAGameMatchup>> GetNflGamesByWeekAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns true if the given league code is supported.</summary>
        bool IsSupported(string league);
    }
}
