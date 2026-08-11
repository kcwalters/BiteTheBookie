using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.ViewModels
{
    public class VideoViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "A YouTube URL or video ID is required.")]
        [Display(Name = "YouTube URL or ID")]
        [StringLength(500)]
        public string YouTubeUrl { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Category { get; set; }

        [Display(Name = "Published")]
        public bool IsPublished { get; set; } = true;

        [Display(Name = "Featured (plays at top of home page)")]
        public bool IsFeatured { get; set; }

        [Display(Name = "Sort Order")]
        public int SortOrder { get; set; }
    }

    public class VideoListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string YouTubeId { get; set; } = string.Empty;
        public string? Category { get; set; }
        public bool IsPublished { get; set; }
        public bool IsFeatured { get; set; }
        public int SortOrder { get; set; }
        public string EnteredBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public string EmbedUrl => $"https://www.youtube.com/embed/{YouTubeId}";
        public string ThumbnailUrl => $"https://i.ytimg.com/vi/{YouTubeId}/hqdefault.jpg";
    }

    public class AdminVideosListViewModel
    {
        public IReadOnlyList<VideoListItemViewModel> Videos { get; set; } = new List<VideoListItemViewModel>();
    }
}
