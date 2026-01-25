namespace BiteTheBookie.Services.Implementations;

public class EspnNewsOptions
{
    public string FeedUrl { get; set; } = "https://www.espn.com/espn/rss/news";
    public int CacheSeconds { get; set; } = 60;
    public int MaxItems { get; set; } = 10;
}
