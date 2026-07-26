using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    /// <summary>
    /// Fetches an NBA schedule for a specific calendar date directly from the ESPN
    /// scoreboard API, returning accurate teams, start times, venues, and live status.
    /// </summary>
    public interface INBAScheduleService
    {
        /// <summary>
        /// Returns the NBA games scheduled for the given date (local date interpreted
        /// against the provider's calendar). Games are ordered by start time and each
        /// carries its Scheduled/Live/Final status.
        /// </summary>
        Task<List<NBAGameMatchup>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    }
}
