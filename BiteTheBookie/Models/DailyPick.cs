using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.Models;

/// <summary>
/// An automated, AI-generated betting pick for a single game. Distinct from
/// <see cref="ExpertPick"/> (which is authored by humans) so machine-generated
/// content carries its own provenance and can be regenerated idempotently.
/// </summary>
public class DailyPick
{
    public int Id { get; set; }

    /// <summary>Stable game identifier from the schedule source (e.g. "bos-mil-20260406").</summary>
    [MaxLength(100)]
    public string GameId { get; set; } = string.Empty;

    /// <summary>League code: NFL, NBA, NHL, MLB, CFB, or CBB.</summary>
    [MaxLength(10)]
    public string League { get; set; } = string.Empty;

    [MaxLength(150)]
    public string AwayTeamName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string HomeTeamName { get; set; } = string.Empty;

    /// <summary>Scheduled kickoff/tipoff time (UTC).</summary>
    public DateTime GameTime { get; set; }

    /// <summary>The calendar date the game commences, in the league (US Eastern) time zone.</summary>
    public DateTime GameDate { get; set; }

    /// <summary>NFL week number when applicable; null for daily-scheduled leagues.</summary>
    public int? Week { get; set; }

    /// <summary>Recommended bet type, e.g. "Spread", "Moneyline", or "Total".</summary>
    [MaxLength(40)]
    public string PickType { get; set; } = string.Empty;

    /// <summary>The actual selection, e.g. "Home -3.5" or "Over 44.5".</summary>
    [MaxLength(200)]
    public string PickSelection { get; set; } = string.Empty;

    /// <summary>AI confidence on a 1-100 scale.</summary>
    public int Confidence { get; set; }

    /// <summary>Predicted final score for the away team, when the model supplies one.</summary>
    public int? PredictedAwayScore { get; set; }

    /// <summary>Predicted final score for the home team, when the model supplies one.</summary>
    public int? PredictedHomeScore { get; set; }

    /// <summary>Full AI-generated rationale.</summary>
    public string? Analysis { get; set; }

    /// <summary>Model/deployment name used to produce the pick.</summary>
    [MaxLength(100)]
    public string GeneratedBy { get; set; } = "AI";

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
