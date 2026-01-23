public class SportsHomeViewModel
{
    public FeaturedPickViewModel FeaturedPick { get; set; }
    public List<ExpertViewModel> TopExperts { get; set; }
    public List<GameViewModel> TrendingGames { get; set; }
    public List<ArticleViewModel> LatestArticles { get; set; }
}

public class FeaturedPickViewModel
{
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string ImageUrl { get; set; }
    public string PickSummary { get; set; }
    public string ActionUrl { get; set; }
}


public class ExpertViewModel
{
    public string Name { get; set; }
    public string Record { get; set; }
    public string AvatarUrl { get; set; }
}

public class GameViewModel
{
    public string TeamA { get; set; }
    public string TeamB { get; set; }
    public string Spread { get; set; }
    public string StartTime { get; set; }
}

public class ArticleViewModel
{
    public string Title { get; set; }
    public string ThumbnailUrl { get; set; }
    public string Published { get; set; }
}
