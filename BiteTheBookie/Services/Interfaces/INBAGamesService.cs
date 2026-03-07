using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface INBAGamesService
    {
        Task<List<NBAGameMatchup>> GetUpcomingNBAGamesAsync(CancellationToken cancellationToken = default);
    }
}
