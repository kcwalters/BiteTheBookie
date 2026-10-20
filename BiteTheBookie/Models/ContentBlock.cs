using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.Models;

/// <summary>
/// An admin-authored block of raw HTML (hero/jumbo/promo markup) that is stored in the
/// database and rendered on a selected page and region, optionally targeted to a specific
/// sport/league and/or team.
/// </summary>
public class ContentBlock
{
    public int Id { get; set; }

    /// <summary>
    /// Admin-friendly label used to identify this block in the admin list.
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The page this block targets (e.g. "Home", "NFL", "Odds"). Use "*" to show on every page.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string PageKey { get; set; } = string.Empty;

    /// <summary>
    /// The region of the page the block renders in (e.g. "top", "hero", "sidebar", "bottom").
    /// </summary>
    [Required]
    [StringLength(100)]
    public string RegionKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional sport/league target (e.g. "NFL", "NBA"). Blank = applies to any sport.
    /// </summary>
    [StringLength(50)]
    public string? Sport { get; set; }

    /// <summary>
    /// Optional team target (e.g. "Bengals"). Blank = applies to any team.
    /// </summary>
    [StringLength(100)]
    public string? Team { get; set; }

    /// <summary>
    /// The raw HTML rendered into the region. Authored by trusted admins only.
    /// </summary>
    [Required]
    public string HtmlContent { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;

    /// <summary>
    /// Lower numbers sort first within a region.
    /// </summary>
    public int SortOrder { get; set; }

    public string EnteredBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
