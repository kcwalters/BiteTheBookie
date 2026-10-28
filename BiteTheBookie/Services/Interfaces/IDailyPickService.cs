using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces;

/// <summary>
/// Generates and persists automated, AI-powered daily picks for every supported
/// league currently in season. Picks are produced for games in the current and
/// upcoming days/weeks and stored for paid-tier users.
/// </summary>
public interface IDailyPickService
{
    /// <summary>
    /// Discovers in-season leagues, enumerates their upcoming games, generates an
    /// AI pick for each, and upserts the results. Idempotent per game. Returns the
    /// number of picks created or refreshed.
    /// </summary>
    Task<int> GenerateDailyPicksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns stored picks with a game date on or after <paramref name="fromDate"/>
    /// (league time zone), ordered by date then league. Optionally filtered by league.
    /// </summary>
    Task<List<DailyPick>> GetUpcomingPicksAsync(DateTime fromDate, string? league = null, CancellationToken cancellationToken = default);
}
