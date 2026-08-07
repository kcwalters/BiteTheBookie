using BiteTheBookie.Services.Implementations;
using BiteTheBookie.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace BiteTheBookie
{
    /// <summary>
    /// Provides DI extension methods to register Sports Ticker services and typed HttpClients.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers ticker options, typed HttpClients, and service interfaces for NFL, NBA, and NHL.
        /// </summary>
        /// <param name="services">The service collection to add registrations to.</param>
        /// <param name="configuration">Application configuration used to bind <see cref="SportsTickerOptions"/> and set HttpClient base addresses.</param>
        /// <returns>The original <see cref="IServiceCollection"/> for chaining.</returns>
        /// <remarks>
        /// This method binds the <c>SportsTicker</c> section to <see cref="SportsTickerOptions"/> and configures
        /// typed HttpClients for each league service. If a base URL is provided in options, it is applied to the client.
        /// </remarks>
        public static IServiceCollection AddSportsTickers(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind once and consume via IOptions<T> in HttpClient configuration
            services.Configure<SportsTickerOptions>(configuration.GetSection("SportsTicker"));

            services.AddHttpClient<INFLScoresService, NFLScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NflApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NflApiBaseUrl);
                ApplyEspnHeaders(client);
            });

            services.AddHttpClient<INBAScoresService, NBAScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NbaApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NbaApiBaseUrl);
                ApplyEspnHeaders(client);
            });

            services.AddHttpClient<INHLScoresService, NHLScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NhlApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NhlApiBaseUrl);
                ApplyEspnHeaders(client);
            });

            services.AddHttpClient<INCAAScoresService, NCAAScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NcaaMensBasketballApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NcaaMensBasketballApiBaseUrl);
                ApplyEspnHeaders(client);
            });

            services.AddHttpClient<ICFBScoresService, CFBScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NcaaFootballApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NcaaFootballApiBaseUrl);
                ApplyEspnHeaders(client);
            });

            return services;
        }

        /// <summary>
        /// ESPN's public scoreboard API returns 403 for requests without a browser-like
        /// User-Agent. Apply standard browser headers so the calls succeed.
        /// </summary>
        internal static void ApplyEspnHeaders(HttpClient client)
        {
            if (!client.DefaultRequestHeaders.UserAgent.TryParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"))
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            }
            client.DefaultRequestHeaders.Accept.TryParseAdd("application/json, text/plain, */*");
        }
    }
}
