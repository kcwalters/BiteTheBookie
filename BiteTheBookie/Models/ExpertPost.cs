using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiteTheBookie.Models;

public class ExpertPost
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required")]
    [StringLength(300, MinimumLength = 5, 
        ErrorMessage = "Title must be between 5 and 300 characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content is required")]
    [StringLength(5000, MinimumLength = 20, 
        ErrorMessage = "Content must be between 20 and 5000 characters")]
    public string Content { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Summary must not exceed 200 characters")]
    public string? Summary { get; set; }

    [StringLength(200, ErrorMessage = "Category must not exceed 200 characters")]
    public string? Category { get; set; }

    /// <summary>
    /// User ID of the expert who created this post
    /// </summary>
    [Required]
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to ApplicationUser
    /// </summary>
    [ForeignKey("AuthorId")]
    public virtual ApplicationUser? Author { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Whether the post is published (visible to users)
    /// </summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>
    /// Number of views this post has received
    /// </summary>
    public int ViewCount { get; set; } = 0;
}
