using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;

namespace BiteTheBookie.Services.Implementations
{
    public class StaticNewsService : INewsService
    {
        public Task<IEnumerable<NewsItemViewModel>> GetLatestNewsAsync(int count = 5)
        {
            return Task.FromResult(Enumerable.Empty<NewsItemViewModel>());
        }

        public Task<NewsItemViewModel> GetNewsByIdAsync(int id)
        {
            return Task.FromResult<NewsItemViewModel?>(null!);
        }
    }
}
