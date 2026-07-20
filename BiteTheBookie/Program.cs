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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    options.AddPolicy("PremiumOnly", policy =>
        policy.RequireClaim("SubscriptionTier", "Premium", "VIP"));

    options.AddPolicy("VIPOnly", policy =>
        policy.RequireClaim("SubscriptionTier", "VIP"));

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

// Bet slip
builder.Services.AddScoped<IBetSlipService, BetSlipService>();

// MLB
builder.Services.AddHttpClient<IMLBGamesService, MLBGamesService>(c =>
{
    c.BaseAddress = new Uri("https://statsapi.mlb.com/api/v1/");
});

// Tickers (NFL, NBA, NHL, NCAA via extension method)
builder.Services.Configure<SportsTickerOptions>(builder.Configuration.GetSection("SportsTicker"));
builder.Services.AddSportsTickers(builder.Configuration);

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

// ESPN API Client
builder.Services.AddHttpClient<EspnApiClient>();

// Razor Pages
builder.Services.AddRazorPages();

// Caching
builder.Services.AddMemoryCache();

var app = builder.Build();

// Apply pending migrations and seed roles on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        string[] roles = { "Admin", "Premium", "VIP", "Free" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            var adminPassword = configuration["SeedAdmin:Password"];

            if (adminUser is null && !string.IsNullOrWhiteSpace(adminPassword))
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(adminUser, adminPassword);
            }

            if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration/seeding failed. The app will still start.");

        // In Development, surface migration failures immediately rather than
        // letting the app start in a broken state (e.g. missing DataProtectionKeys table).
        if (app.Environment.IsDevelopment())
            throw;
    }
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();