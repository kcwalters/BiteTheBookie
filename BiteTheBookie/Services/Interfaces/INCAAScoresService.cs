using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface INCAAScoresService
    {
        Task<IReadOnlyList<NCAATickerView>> GetGamesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NCAATickerView>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    }
}
