namespace BiteTheBookie.ViewModels
{
    public class NewsItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string ArticleUrl { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
    }
}
