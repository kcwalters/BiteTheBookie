using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.Models;

/// <summary>
/// One row per calendar day on which the automated Expert Picks generation job ran.
/// Acts as a distributed run-lock so that, across multiple hosting replicas, only the
/// instance that successfully inserts the <see cref="RunDate"/> row performs generation
/// for that day. Kept separate from <see cref="DailyPickRun"/> so the two automated
/// jobs never interfere with each other's locks.
/// </summary>
public class ExpertPickRun
{
    public int Id { get; set; }

    /// <summary>The league-timezone date this run covers (date-only). Unique.</summary>
    public DateTime RunDate { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public int GamesProcessed { get; set; }

    public int PicksGenerated { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
