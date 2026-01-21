namespace BiteTheBookie.Models;

public readonly record struct NFLTickerView(
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
