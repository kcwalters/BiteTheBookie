using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface IGameSimulationService
    {
        Task<string> GenerateGameSimulationAsync(
            string homeTeam,
            string awayTeam,
            string league,
            NBATeamRoster? homeRoster = null,
            NBATeamRoster? awayRoster = null,
            List<PlayerInjuryReport>? injuries = null,
            DateTime? gameTime = null,
            CancellationToken cancellationToken = default,
            string? homeProbablePitcher = null,
            string? awayProbablePitcher = null);
    }
}


