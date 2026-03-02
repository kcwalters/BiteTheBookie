using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface INBAGamesService
    {
        Task<List<NBAGameMatchup>> GetUpcomingGamesAsync(CancellationToken cancellationToken = default);
    }
}
