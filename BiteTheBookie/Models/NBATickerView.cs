namespace BiteTheBookie.Models;

public readonly record struct NBATickerView(
    string AwayTeam,
    string HomeTeam,
    int? AwayScore,
    int? HomeScore,
    string AwayLogo,
    string HomeLogo,
    string StatusText,
    bool IsLive,
    bool IsFinal,
    string? EventId
);
