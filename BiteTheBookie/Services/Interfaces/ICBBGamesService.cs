using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface ICBBGamesService
    {
        Task<List<CBBGameMatchup>> GetUpcomingGamesAsync(CancellationToken cancellationToken = default);
    }
}
