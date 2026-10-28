namespace BiteTheBookie.Services.Interfaces;

/// <summary>
/// Generates and persists automated, AI-powered Expert Picks for every supported
/// league currently in season. Picks are produced for games in the upcoming days,
/// upserted idempotently by league/game, and stored for all paid-tier users so they
/// all see the same scheduled or manually-refreshed picks.
/// </summary>
public interface IExpertPickAutomationService
{
    /// <summary>
    /// Discovers in-season leagues, enumerates their upcoming games within the
    /// lookahead window, generates an AI Expert Pick for each, and upserts the
    /// results. Idempotent per game. Returns the number of picks created or refreshed.
    /// </summary>
    Task<int> GenerateExpertPicksAsync(CancellationToken cancellationToken = default);
}
