using System.Collections.Generic;
using BiteTheBookie.ViewModels;
namespace BiteTheBookie.ViewModels
{
    public class HomePageViewModel
    {
        public IEnumerable<HeroOddViewModel> HeroOdds { get; set; }
        public IEnumerable<NewsItemViewModel> NewsFeed { get; set; }
        public IEnumerable<LiveOddsViewModel> LiveOdds { get; set; }
        public LeagueOddsViewModel LeagueOdds { get; set; }
        public IEnumerable<ExpertPickSummary> ExpertPicks { get; set; }
        public VideoListItemViewModel FeaturedVideo { get; set; }
        public IEnumerable<VideoListItemViewModel> RecentVideos { get; set; }

        public HomePageViewModel()
        {
            HeroOdds = new List<HeroOddViewModel>();
            NewsFeed = new List<NewsItemViewModel>();
            LiveOdds = new List<LiveOddsViewModel>();
            LeagueOdds = new LeagueOddsViewModel();
            ExpertPicks = new List<ExpertPickSummary>();
            RecentVideos = new List<VideoListItemViewModel>();
        }
    }
}
