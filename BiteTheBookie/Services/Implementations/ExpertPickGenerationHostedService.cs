using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Services.Implementations;

/// <summary>
/// Background scheduler that triggers automated Expert Picks generation at a fixed
/// local time (default 4:00 AM US Eastern). A unique <see cref="ExpertPickRun"/> row
/// acts as a distributed lock so that, across multiple hosting replicas, only one
/// instance performs generation per calendar day.
/// </summary>
public class ExpertPickGenerationHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpertPickGenerationHostedService> _logger;
    private readonly TimeSpan _runTime;

    public ExpertPickGenerationHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ExpertPickGenerationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Configurable "HH:mm" local (Eastern) time; defaults to 04:00.
        var configured = configuration["ExpertPicks:RunTime"];
        _runTime = TimeSpan.TryParse(configured, out var parsed) ? parsed : new TimeSpan(4, 0, 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = TimeUntilNextRun();
                _logger.LogInformation("ExpertPicks scheduler: next run in {Delay} (at {RunTime} Eastern).", delay, _runTime);
                await Task.Delay(delay, stoppingToken);

                await RunIfOwnerAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExpertPicks scheduler: unexpected error; retrying in 5 minutes.");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private TimeSpan TimeUntilNextRun()
    {
        var tz = NBAGameMatchup.LeagueTimeZone;
        var nowEastern = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayRun = nowEastern.Date.Add(_runTime);
        var nextRun = nowEastern < todayRun ? todayRun : todayRun.AddDays(1);
        var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextRun, DateTimeKind.Unspecified), tz);
        var delay = nextRunUtc - DateTime.UtcNow;
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    /// <summary>
    /// Attempts to claim the run for today by inserting a unique ExpertPickRun row.
    /// Only the replica that wins the insert performs generation.
    /// </summary>
    private async Task RunIfOwnerAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var runDate = NBAGameMatchup.LeagueToday;

        if (await db.ExpertPickRuns.AnyAsync(r => r.RunDate == runDate, cancellationToken))
        {
            _logger.LogInformation("ExpertPicks scheduler: run for {RunDate:d} already exists; skipping.", runDate);
            return;
        }

        var run = new ExpertPickRun { RunDate = runDate, StartedAt = DateTime.UtcNow };
        db.ExpertPickRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another replica won the race (unique index on RunDate).
            _logger.LogInformation("ExpertPicks scheduler: another instance claimed {RunDate:d}; skipping.", runDate);
            return;
        }

        try
        {
            var pickService = scope.ServiceProvider.GetRequiredService<IExpertPickAutomationService>();
            var generated = await pickService.GenerateExpertPicksAsync(cancellationToken);

            run.PicksGenerated = generated;
            run.CompletedAt = DateTime.UtcNow;
            run.Notes = $"Generated {generated} Expert Picks.";
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExpertPicks scheduler: generation failed for {RunDate:d}.", runDate);
            run.CompletedAt = DateTime.UtcNow;
            run.Notes = $"Failed: {ex.Message}";
            try { await db.SaveChangesAsync(cancellationToken); } catch { /* best effort */ }
        }
    }
}
