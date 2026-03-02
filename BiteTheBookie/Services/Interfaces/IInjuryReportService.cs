using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface IInjuryReportService
    {
        Task<List<PlayerInjuryReport>> GetCurrentInjuriesAsync(string teamCode, CancellationToken cancellationToken = default);
        Task<List<PlayerInjuryReport>> GetCurrentInjuriesForGameAsync(string awayTeamCode, string homeTeamCode, DateTime gameTime, CancellationToken cancellationToken = default);
    }
}
