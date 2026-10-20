using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.ViewModels
{
    public class ContentBlockViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(200)]
        [Display(Name = "Name (admin label)")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose which page this block appears on.")]
        [StringLength(100)]
        [Display(Name = "Page")]
        public string PageKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose the region of the page.")]
        [StringLength(100)]
        [Display(Name = "Region")]
        public string RegionKey { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Sport / League (optional)")]
        public string? Sport { get; set; }

        [StringLength(100)]
        [Display(Name = "Team (optional)")]
        public string? Team { get; set; }

        [Required(ErrorMessage = "HTML content is required.")]
        [Display(Name = "HTML content")]
        public string HtmlContent { get; set; } = string.Empty;

        [Display(Name = "Published")]
        public bool IsPublished { get; set; } = true;

        [Display(Name = "Sort Order")]
        public int SortOrder { get; set; }
    }

    public class ContentBlockListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PageKey { get; set; } = string.Empty;
        public string RegionKey { get; set; } = string.Empty;
        public string? Sport { get; set; }
        public string? Team { get; set; }
        public bool IsPublished { get; set; }
        public int SortOrder { get; set; }
        public string EnteredBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AdminContentBlocksListViewModel
    {
        public IReadOnlyList<ContentBlockListItemViewModel> Blocks { get; set; } = new List<ContentBlockListItemViewModel>();
    }
}
