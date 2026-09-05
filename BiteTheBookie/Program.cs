using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using BiteTheBookie;
using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services;
using BiteTheBookie.Services.Implementations;
using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenAI.Chat;
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

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<StartupDependencyHealthCheck>("startup_dependencies", tags: new[] { "ready" });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); // ensures DataProtectionKeys and all pending migrations are applied

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
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.Run();