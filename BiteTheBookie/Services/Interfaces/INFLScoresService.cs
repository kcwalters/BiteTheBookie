using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{

    public interface INFLScoresService
    {
        Task<IReadOnlyList<NFLTickerView>> GetGamesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NFLTickerView>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    }
}