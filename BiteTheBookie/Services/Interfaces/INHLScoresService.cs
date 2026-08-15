using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{

    public interface INHLScoresService
    {
        Task<IReadOnlyList<NHLTickerView>> GetGamesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NHLTickerView>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    }
}