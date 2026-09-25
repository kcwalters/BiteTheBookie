using Azure;
using Azure.AI.OpenAI;
using BiteTheBookie;
using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services;
using BiteTheBookie.Services.Implementations;
using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using Serilog.Events;
var builder = WebApplication.CreateBuilder(args); 
 
// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// Persist Data Protection keys to the database so they survive container restarts
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();
 
// Identity with ApplicationUser and Roles
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Authorization policies for Free vs Paid access
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ProOnly", policy =>
        policy.RequireClaim("SubscriptionTier", "Pro", "AllAccess"));

    options.AddPolicy("AllAccessOnly", policy =>
        policy.RequireClaim("SubscriptionTier", "AllAccess"));

    options.AddPolicy("RegisteredUser", policy =>
        policy.RequireAuthenticatedUser());
});

// MVC
builder.Services.AddControllersWithViews();

// Behind Azure's TLS-terminating proxy the app only listens on HTTP, so the
// HTTPS redirection middleware cannot auto-discover an HTTPS port and logs
// "Failed to determine the https port for redirect". Pin it to 443 so any
// redirect that does fire targets the correct public https port.
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 443;
});

// Azure OpenAI ChatClient — register once for all services
var aoaiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var aoaiApiKey = builder.Configuration["AzureOpenAI:ApiKey"];
var aoaiDeployment = builder.Configuration["AzureOpenAI:DeploymentName"];

if (!string.IsNullOrEmpty(aoaiEndpoint) && !string.IsNullOrEmpty(aoaiApiKey) && !string.IsNullOrEmpty(aoaiDeployment))
{
    var azureOpenAIClient = new AzureOpenAIClient(new Uri(aoaiEndpoint), new AzureKeyCredential(aoaiApiKey));
    var chatClient = azureOpenAIClient.GetChatClient(aoaiDeployment);
    builder.Services.AddSingleton(chatClient);
}
else
{
    // Register a null instance so services can gracefully degrade
    builder.Services.AddSingleton<ChatClient?>(sp => null);
}

// Odds API options + client
builder.Services.Configure<OddsApiOptions>(builder.Configuration.GetSection("OddsApi"));
builder.Services.AddHttpClient<TheOddsApiClient>();
builder.Services.AddScoped<TheOddsApiClient>();
builder.Services.AddHttpClient<OddsService>();
builder.Services.AddScoped<IOddsService>(sp => sp.GetRequiredService<OddsService>());

// News (ESPN RSS overrides the stub — only register EspnRssNewsService)
builder.Services.Configure<EspnNewsOptions>(builder.Configuration.GetSection("EspnNews"));
builder.Services.AddHttpClient<EspnRssNewsService>();
builder.Services.AddScoped<INewsService>(sp => sp.GetRequiredService<EspnRssNewsService>());

builder.Services.AddHttpClient<PayPalService>();

// MLB
builder.Services.AddHttpClient<IMLBGamesService, MLBGamesService>(c =>
{
    c.BaseAddress = new Uri("https://statsapi.mlb.com/api/v1/");
});

// Tickers (NFL, NBA, NHL, NCAA via extension method)
builder.Services.Configure<SportsTickerOptions>(builder.Configuration.GetSection("SportsTicker"));
builder.Services.AddSportsTickers(builder.Configuration);

// Daily Fantasy Football (DFS)
builder.Services.AddFantasyFootball(builder.Configuration);
 
// Game services
builder.Services.AddScoped<IGameSimulationService, GameSimulationService>();
builder.Services.AddScoped<INBARosterService, NBARosterService>();
builder.Services.AddScoped<INBAGamesService, NBAGamesService>();
builder.Services.AddScoped<ISpreadAnalysisService, SpreadAnalysisService>();
builder.Services.AddScoped<IInjuryReportService, InjuryReportService>();
builder.Services.AddScoped<ICBBGamesService, CBBGamesService>();
builder.Services.AddScoped<ICBBRosterService, CBBRosterService>();
builder.Services.AddScoped<ICFBGamesService, CFBGamesService>();
builder.Services.AddScoped<INBAScoresService, NBAScoresService>();

// Date-aware NBA schedule (ESPN scoreboard by date) for Scores & Simulations
builder.Services.AddHttpClient<INBAScheduleService, NBAScheduleService>();

// Date-aware schedule for ALL sports (ESPN scoreboard by date)
builder.Services.AddHttpClient<ILeagueScheduleService, EspnScheduleService>();

// ESPN API Client
builder.Services.AddHttpClient<EspnApiClient>();

// Razor Pages
builder.Services.AddRazorPages();

// Caching
builder.Services.AddMemoryCache();

// Session (required by MembershipController.Register which stashes PendingRegistration in HttpContext.Session).
// In Azure Container Apps the app can run multiple replicas and containers are recycled between requests.
// An in-memory session store (AddDistributedMemoryCache) lives in a single process, so a follow-up
// request served by another instance (or after a restart) can't see the session and the checkout flow
// reports "Your session expired". Back the session with SQL Server so it is shared across all instances.
// In Development (single instance) fall back to in-memory to avoid needing the cache table locally.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = connectionString;
        options.SchemaName = "dbo";
        options.TableName = "SessionCache";
    });
}

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // Behind the TLS-terminating proxy the public request is HTTPS; require the session
    // cookie to be sent over secure connections and allow it to survive the PayPal redirect round-trip.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Serilog: write logs to console and to SQL Server (Logs table). Uses DefaultConnection.
var sqlConnectionString = connectionString;
var sinkOptions = new MSSqlServerSinkOptions { TableName = "Logs", AutoCreateSqlTable = true };

// Configurable minimum level. Set "Serilog:MinimumLevel" to "Error" to log errors only,
// or "Information" (default) to capture all logging. Any valid LogEventLevel is accepted.
var configuredLevel = builder.Configuration["Serilog:MinimumLevel"];
var minimumLevel = Enum.TryParse<LogEventLevel>(configuredLevel, ignoreCase: true, out var parsedLevel)
    ? parsedLevel
    : LogEventLevel.Information;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(minimumLevel)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.MSSqlServer(sqlConnectionString, sinkOptions)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); // ensures DataProtectionKeys and all pending migrations are applied

    // Ensure the distributed session cache table exists (used by AddDistributedSqlServerCache in
    // non-Development environments). The SQL cache provider does not create this table itself, so we
    // create it idempotently here with the exact schema/index it expects.
    if (!app.Environment.IsDevelopment())
    {
        const string createSessionCacheTable = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SessionCache' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[SessionCache] (
        [Id] nvarchar(449) NOT NULL,
        [Value] varbinary(max) NOT NULL,
        [ExpiresAtTime] datetimeoffset NOT NULL,
        [SlidingExpirationInSeconds] bigint NULL,
        [AbsoluteExpiration] datetimeoffset NULL,
        CONSTRAINT [PK_SessionCache] PRIMARY KEY ([Id])
    );
    CREATE NONCLUSTERED INDEX [Index_ExpiresAtTime] ON [dbo].[SessionCache] ([ExpiresAtTime]);
END";
        db.Database.ExecuteSqlRaw(createSessionCacheTable);
    }

    // Ensure the subscription/access roles exist so AddToRoleAsync never fails.
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var roleName in new[] { "Free", "Pro", "AllAccess", "Admin" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
    }

    // Diagnostic: validate configured PayPal billing plans exist and are ACTIVE.
    // Never throws; only logs warnings so misconfiguration is caught at startup, not at checkout.
    var payPalService = scope.ServiceProvider.GetRequiredService<PayPalService>();
    await payPalService.ValidateConfiguredPlansAsync();
}

// Configure the HTTP request pipeline.

// Azure Container Apps (and most cloud hosts) sit behind a reverse proxy that
// terminates TLS and forwards requests to the container over plain HTTP.
// Honor X-Forwarded-Proto/For so the app knows the original request was HTTPS.
// Without this, HTTPS redirection loops and auth cookies fail to round-trip,
// which sends logged-in users back to the login screen repeatedly.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // Allow an unlimited number of forwarding hops. Azure's reverse proxy is not
    // at a fixed/known address, so we cannot enumerate it as a KnownProxy.
    ForwardLimit = null
};

// By default the middleware only trusts loopback proxies. Clearing both lists
// makes it accept the X-Forwarded-* headers from Azure's front-end proxy so
// Request.Scheme is correctly reported as https. Without this the headers are
// ignored, HTTPS redirection can't determine the port, and outbound return
// URLs (e.g. the PayPal redirect) are built with http instead of https.
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

 
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
