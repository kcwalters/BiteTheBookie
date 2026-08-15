using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{

    public interface INBAScoresService
    {
        Task<IReadOnlyList<NBATickerView>> GetGamesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NBATickerView>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    }
}