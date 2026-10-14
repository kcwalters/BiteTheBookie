using BiteTheBookie.Services.Implementations;
using BiteTheBookie.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

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
            }).AddEspnResilience();
            services.AddScoped<INFLScoresService, NFLScoresService>();

            services.AddHttpClient<NBAScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NbaApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NbaApiBaseUrl);
            }).AddEspnResilience();
            services.AddScoped<INBAScoresService, NBAScoresService>();

            services.AddHttpClient<NHLScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NhlApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NhlApiBaseUrl);
            }).AddEspnResilience();
            services.AddScoped<INHLScoresService, NHLScoresService>();

            services.AddHttpClient<NCAAScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NcaaMensBasketballApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NcaaMensBasketballApiBaseUrl);
            }).AddEspnResilience();
            services.AddScoped<INCAAScoresService, NCAAScoresService>();

            services.AddHttpClient<CFBScoresService>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SportsTickerOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.NcaaFootballApiBaseUrl))
                    client.BaseAddress = new Uri(opts.NcaaFootballApiBaseUrl);
            }).AddEspnResilience();
            services.AddScoped<ICFBScoresService, CFBScoresService>();

            return services;
        }

        /// <summary>
        /// Applies a shared resilience pipeline to an ESPN-backed HttpClient: short per-attempt
        /// timeout, retry with exponential backoff + jitter on transient failures (including
        /// HTTP 429/5xx), and a circuit breaker so a sustained ESPN outage or block stops
        /// hammering the host and fails fast instead.
        /// </summary>
        public static IHttpClientBuilder AddEspnResilience(this IHttpClientBuilder builder)
        {
            builder.AddResilienceHandler("espn", pipeline =>
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(300)
                });

                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(15)
                });

                pipeline.AddTimeout(TimeSpan.FromSeconds(10));
            });

            return builder;
        }

        /// <summary>
        /// Registers Daily Fantasy Football (DFS) services: PPR scoring options, the scoring
        /// service, and the ESPN-backed data service (slate, player pool, box-score points).
        /// </summary>
        /// <param name="services">The service collection to add registrations to.</param>
        /// <param name="configuration">Application configuration used to bind <see cref="Models.Fantasy.FantasyScoringOptions"/>.</param>
        /// <returns>The original <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection AddFantasyFootball(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<Models.Fantasy.FantasyScoringOptions>(
                configuration.GetSection(Models.Fantasy.FantasyScoringOptions.SectionName));

            services.AddScoped<IFantasyScoringService, FantasyScoringService>();

            services.AddHttpClient<INflFantasyDataService, NflFantasyDataService>(client =>
            {
                client.BaseAddress = new Uri("https://site.web.api.espn.com/");
            }).AddEspnResilience();

            return services;
        }
    }
}
