using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{

    public interface INHLScoresService
    {
        Task<IReadOnlyList<NHLTickerView>> GetGamesAsync(CancellationToken cancellationToken = default);
    }
}