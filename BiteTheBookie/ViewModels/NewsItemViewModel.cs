namespace BiteTheBookie.ViewModels
{
    public class NewsItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Excerpt { get; set; }
        public string ImageUrl { get; set; }
        public string ArticleUrl { get; set; }
        public DateTime PublishedAt { get; set; }
    }

}
