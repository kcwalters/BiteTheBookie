using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.Models;

public class SiteVideo
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// The raw YouTube URL or 11-character video ID entered by the admin.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string YouTubeUrl { get; set; } = string.Empty;

    /// <summary>
    /// The parsed 11-character YouTube video ID used for embedding/thumbnails.
    /// </summary>
    [Required]
    [StringLength(20)]
    public string YouTubeId { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Category { get; set; }

    public bool IsPublished { get; set; } = true;

    public bool IsFeatured { get; set; }

    /// <summary>
    /// Lower numbers sort first within the video lists.
    /// </summary>
    public int SortOrder { get; set; }

    public string EnteredBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
