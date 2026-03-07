using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface ICBBGamesService
    {
        Task<List<CBBGameMatchup>> GetUpcomingCBBGamesAsync(CancellationToken cancellationToken = default);
    }
}
