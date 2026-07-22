namespace BiteTheBookie.Services.Implementations
{
    public class SportsTickerOptions
    {
        public string? NflApiBaseUrl { get; set; }
        public string? NbaApiBaseUrl { get; set; }
        public string? NhlApiBaseUrl { get; set; }
        public string? NcaaMensBasketballApiBaseUrl { get; set; }
        public string? NcaaFootballApiBaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public int PollingIntervalSeconds { get; set; } = 60;
    }
}
