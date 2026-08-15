using BiteTheBookie.Models.MLB;

namespace BiteTheBookie.Services.Interfaces
{
    public interface IMLBGamesService
    {
        Task<List<Game>> GetTodayGamesAsync();
        Task<IReadOnlyList<Game>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    }

}
