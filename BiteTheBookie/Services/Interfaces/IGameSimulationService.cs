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
            CancellationToken cancellationToken = default);
    }
}

