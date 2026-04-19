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

    // NFLOddsViewModel.cs
    public class NFLOddsViewModel
    {
        public string GameId { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public DateTime CommenceTime { get; set; }
        public string VenueTimeZoneId { get; set; } = "Eastern Standard Time";
        public string AwayMoneyline { get; set; } = "";
        public string HomeMoneyline { get; set; } = "";
        public string AwaySpread { get; set; } = "";
        public string AwaySpreadPrice { get; set; } = "";
        public string HomeSpread { get; set; } = "";
        public string HomeSpreadPrice { get; set; } = "";
        public string OverPoint { get; set; } = "";
        public string OverPrice { get; set; } = "";
        public string UnderPoint { get; set; } = "";
        public string UnderPrice { get; set; } = "";
        public string Bookmaker { get; set; } = "DraftKings";
    }

    // NBAOddsViewModel.cs
    public class NBAOddsViewModel
    {
        public string GameId { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public DateTime CommenceTime { get; set; }
        public string VenueTimeZoneId { get; set; } = "Eastern Standard Time";
        public string AwayMoneyline { get; set; } = "";
        public string HomeMoneyline { get; set; } = "";
        public string AwaySpread { get; set; } = "";
        public string AwaySpreadPrice { get; set; } = "";
        public string HomeSpread { get; set; } = "";
        public string HomeSpreadPrice { get; set; } = "";
        public string OverPoint { get; set; } = "";
        public string OverPrice { get; set; } = "";
        public string UnderPoint { get; set; } = "";
        public string UnderPrice { get; set; } = "";
        public string Bookmaker { get; set; } = "DraftKings";
    }

    // CBBOddsViewModel.cs (College Basketball)
    public class CBBOddsViewModel
    {
        public string GameId { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public DateTime CommenceTime { get; set; }
        public string VenueTimeZoneId { get; set; } = "Eastern Standard Time";
        public string AwayMoneyline { get; set; } = "";
        public string HomeMoneyline { get; set; } = "";
        public string AwaySpread { get; set; } = "";
        public string AwaySpreadPrice { get; set; } = "";
        public string HomeSpread { get; set; } = "";
        public string HomeSpreadPrice { get; set; } = "";
        public string OverPoint { get; set; } = "";
        public string OverPrice { get; set; } = "";
        public string UnderPoint { get; set; } = "";
        public string UnderPrice { get; set; } = "";
        public string Bookmaker { get; set; } = "DraftKings";
    }

    // MLBOddsViewModel.cs (Major League Baseball)
    public class MLBOddsViewModel
    {
        public string GameId { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public DateTime CommenceTime { get; set; }
        public string VenueTimeZoneId { get; set; } = "Eastern Standard Time";
        public string AwayMoneyline { get; set; } = "";
        public string HomeMoneyline { get; set; } = "";
        public string AwaySpread { get; set; } = "";
        public string AwaySpreadPrice { get; set; } = "";
        public string HomeSpread { get; set; } = "";
        public string HomeSpreadPrice { get; set; } = "";
        public string OverPoint { get; set; } = "";
        public string OverPrice { get; set; } = "";
        public string UnderPoint { get; set; } = "";
        public string UnderPrice { get; set; } = "";
        public string Bookmaker { get; set; } = "DraftKings";
    }

    // NHLOddsViewModel.cs (National Hockey League)
    public class NHLOddsViewModel
    {
        public string GameId { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public DateTime CommenceTime { get; set; }
        public string VenueTimeZoneId { get; set; } = "Eastern Standard Time";
        public string AwayMoneyline { get; set; } = "";
        public string HomeMoneyline { get; set; } = "";
        public string AwaySpread { get; set; } = "";
        public string AwaySpreadPrice { get; set; } = "";
        public string HomeSpread { get; set; } = "";
        public string HomeSpreadPrice { get; set; } = "";
        public string OverPoint { get; set; } = "";
        public string OverPrice { get; set; } = "";
        public string UnderPoint { get; set; } = "";
        public string UnderPrice { get; set; } = "";
        public string Bookmaker { get; set; } = "DraftKings";
    }
}





