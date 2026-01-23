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

            services.AddHttpClient<NFLScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NflApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NflApiBaseUrl);
            });
            services.AddScoped<INFLScoresService, NFLScoresService>();

            services.AddHttpClient<NBAScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NbaApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NbaApiBaseUrl);
            });
            services.AddScoped<INBAScoresService, NBAScoresService>();

            services.AddHttpClient<NHLScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NhlApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NhlApiBaseUrl);
            });
            services.AddScoped<INHLScoresService, NHLScoresService>();

            services.AddHttpClient<NCAAScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NcaaMensBasketballApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NcaaMensBasketballApiBaseUrl);
            });
            services.AddScoped<INCAAScoresService, NCAAScoresService>();

            return services;
        }
    }
}
