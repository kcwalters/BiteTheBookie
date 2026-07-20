using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface ICFBGamesService
    {
        Task<List<CFBGameMatchup>> GetUpcomingCFBGamesAsync(CancellationToken cancellationToken = default);
    }
}
