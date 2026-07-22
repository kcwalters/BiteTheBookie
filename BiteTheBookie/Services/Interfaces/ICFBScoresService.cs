using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface ICFBScoresService
    {
        Task<IReadOnlyList<CFBTickerView>> GetGamesAsync(CancellationToken cancellationToken = default);
    }
}
