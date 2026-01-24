using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
namespace BiteTheBookie.Services.Implementations
{
    public class NewsService : INewsService
    {
        public Task<IEnumerable<NewsItemViewModel>> GetLatestNewsAsync(int count = 5)
            => Task.FromResult(Enumerable.Empty<NewsItemViewModel>());

        public Task<NewsItemViewModel> GetNewsByIdAsync(int id)
        {
            // Return a default NewsItemViewModel instance instead of null to match the non-nullable return type
            return Task.FromResult(new NewsItemViewModel());
        }
    }

    public class BetSlipService : IBetSlipService
    {
        private readonly List<BetSlipItemViewModel> _bets
          = new();

        public Task<BetSlipViewModel> GetBetSlipAsync()
            => Task.FromResult(new BetSlipViewModel { Bets = _bets });

        public Task AddBetAsync(AddBetRequest req)
        {
            _bets.Add(new BetSlipItemViewModel
            {
                GameId = req.GameId,
                Selection = req.Selection,
                OddsDecimal = req.OddsDecimal,
                Stake = req.Stake
            });
            return Task.CompletedTask;
        }

        public Task RemoveBetAsync(int gameId)
        {
            _bets.RemoveAll(b => b.GameId == gameId);
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _bets.Clear();
            return Task.CompletedTask;
        }
    }
}