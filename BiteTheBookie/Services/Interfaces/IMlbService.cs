using BiteTheBookie.Models.MLB;

namespace BiteTheBookie.Services.Interfaces
{
    public interface IMlbService
    {
        Task<List<Game>> GetTodayGamesAsync();
    }

}
