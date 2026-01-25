namespace BiteTheBookie.Services.Implementations
{
    public class OddsApiOptions
    {
        public string BaseUrl { get; set; } = "https://api.the-odds-api.com/v4/";
        public string ApiKey { get; set; } = "";
        public string Regions { get; set; } = "us";
        public string Markets { get; set; } = "h2h,spreads,totals";
        public string OddsFormat { get; set; } = "american";
        public int CacheSeconds { get; set; } = 30;
    }
}
