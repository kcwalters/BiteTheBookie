namespace BiteTheBookie.ViewModels
{
    // HeroOddViewModel.cs
    public class HeroOddViewModel
    {
        public int GameId { get; set; }
        public string AwayAbbrev { get; set; }
        public string AwayOdds { get; set; }
        public string HomeAbbrev { get; set; }
        public string HomeOdds { get; set; }
    }

    // LiveOddsViewModel.cs
    public class LiveOddsViewModel
    {
        public int GameId { get; set; }
        public string AwayAbbrev { get; set; }
        public int AwayScore { get; set; }
        public string HomeAbbrev { get; set; }
        public int HomeScore { get; set; }
        public string Status { get; set; }
        public string Moneyline { get; set; }
        public string Spread { get; set; }
        public string Total { get; set; }
    }

    // GameOddsViewModel.cs
    public class GameOddsViewModel
    {
        public int GameId { get; set; }
        public string Away { get; set; }
        public string Home { get; set; }
        public string Moneyline { get; set; }
        public string Spread { get; set; }
        public string Total { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsHighlighted { get; set; }
    }

    // LeagueViewModel.cs
    public class LeagueViewModel
    {
        public string Name { get; set; }
        public IEnumerable<GameOddsViewModel> Games { get; set; }
        = Enumerable.Empty<GameOddsViewModel>();
    }

    // LeagueOddsViewModel.cs
    public class LeagueOddsViewModel
    {
        public IList<LeagueViewModel> Leagues { get; set; }
        = new List<LeagueViewModel>();
    }

}
