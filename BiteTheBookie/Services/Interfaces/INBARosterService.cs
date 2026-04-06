using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface INBARosterService
    {
        /// <summary>Synchronous lookup — returns static fallback data.</summary>
        NBATeamRoster GetTeamRoster(string teamCode);

        /// <summary>
        /// Async lookup — tries ESPN API first, caches the result for 1 hour,
        /// then falls back to static data if the API is unavailable.
        /// </summary>
        Task<NBATeamRoster> GetTeamRosterAsync(string teamCode, CancellationToken cancellationToken = default);
    }
}
