using Azure.Identity;
using BiteTheBookie.Data;
using BiteTheBookie.Services;
using BiteTheBookie.Services.Implementations;
using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The Key Vault URI (set as a non-secret environment variable in the container app).
//var kvUri = Environment.GetEnvironmentVariable("KEY_VAULT_URI");
//if (string.IsNullOrEmpty(kvUri))
//{
//    throw new InvalidOperationException("KEY_VAULT_URI is not set");
//}

//var secretName = "SqlPassword"; // Name of the secret in Key Vault

//var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
//KeyVaultSecret secret = await client.GetSecretAsync(secretName);
//var sqlPassword = secret.Value;

//// Example: build connection string using password retrieved from Key Vault
//var sqlServer = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "your-sql-server.database.windows.net";
//var sqlUser = Environment.GetEnvironmentVariable("SQL_USER") ?? "dbuser";
//var connString = $"Server=tcp:{sqlServer},1433;Initial Catalog=YourDatabase;Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IOddsService, OddsService>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<IBetSlipService, BetSlipService>();

// Typed HTTP client for Odds API
builder.Services.AddHttpClient<IOddsService, OddsService>(c =>
{
    c.BaseAddress = new Uri("https://api.your-odds-provider.com/");
});

builder.Services.AddHttpClient<IMlbService, MlbService>(c =>
{
    c.BaseAddress = new Uri("https://statsapi.mlb.com/api/v1/");
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
