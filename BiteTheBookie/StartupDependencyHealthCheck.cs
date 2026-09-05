using BiteTheBookie.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BiteTheBookie;

public sealed class StartupDependencyHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;

    public StartupDependencyHealthCheck(IConfiguration configuration, ApplicationDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();

        RequireSetting(missing, _configuration.GetConnectionString("DefaultConnection"), "ConnectionStrings:DefaultConnection");
        RequireSetting(missing, _configuration["AzureOpenAI:Endpoint"], "AzureOpenAI:Endpoint");
        RequireSetting(missing, _configuration["AzureOpenAI:ApiKey"], "AzureOpenAI:ApiKey");
        RequireSetting(missing, _configuration["AzureOpenAI:DeploymentName"], "AzureOpenAI:DeploymentName");
        RequireSetting(missing, _configuration["OddsApi:ApiKey"], "OddsApi:ApiKey");

        if (missing.Count > 0)
        {
            return HealthCheckResult.Unhealthy($"Missing required configuration: {string.Join(", ", missing)}");
        }

        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Database connectivity check failed.");
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connectivity check threw an exception.", ex);
        }

        return HealthCheckResult.Healthy("Configuration and database connectivity checks passed.");
    }

    private static void RequireSetting(ICollection<string> missing, string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(key);
        }
    }
}
