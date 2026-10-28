using System.Text.Json;
using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace BiteTheBookie.Services.Implementations;

/// <summary>
/// Produces automated AI-powered Expert Picks for every supported league currently in
/// season and persists them idempotently into the shared <see cref="ExpertPick"/> table
/// consumed by all paid-tier users. Designed to fail per-game/per-league without taking
/// down the application; a null <see cref="ChatClient"/> makes generation a no-op.
/// </summary>
public class ExpertPickAutomationService : IExpertPickAutomationService
{
    // Supported league codes (see ILeagueScheduleService).
    private static readonly string[] DailyLeagues = { "NBA", "NHL", "MLB", "CFB", "CBB" };

    private const string AutomationEnteredBy = "AI Scheduler";

    private readonly ChatClient? _chatClient;
    private readonly ILeagueScheduleService _scheduleService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ExpertPickAutomationService> _logger;
    private readonly int _lookaheadDays;

    public ExpertPickAutomationService(
        ChatClient? chatClient,
        ILeagueScheduleService scheduleService,
        ApplicationDbContext db,
        IConfiguration configuration,
        ILogger<ExpertPickAutomationService> logger)
    {
        _chatClient = chatClient;
        _scheduleService = scheduleService;
        _db = db;
        _logger = logger;
        _lookaheadDays = configuration.GetValue<int>("ExpertPicks:LookaheadDays", 10);
    }

    public async Task<int> GenerateExpertPicksAsync(CancellationToken cancellationToken = default)
    {
        if (_chatClient == null)
        {
            _logger.LogWarning("ExpertPickAutomationService: Azure OpenAI ChatClient is not configured. Skipping generation.");
            return 0;
        }

        var games = await CollectUpcomingGamesAsync(cancellationToken);
        if (games.Count == 0)
        {
            _logger.LogInformation("ExpertPickAutomationService: no upcoming games found across supported leagues.");
            return 0;
        }

        _logger.LogInformation("ExpertPickAutomationService: generating Expert Picks for {Count} upcoming games.", games.Count);

        var generated = 0;
        foreach (var (league, game) in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var pick = await GeneratePickForGameAsync(league, game, cancellationToken);
                if (pick == null)
                    continue;

                await UpsertPickAsync(pick, cancellationToken);
                generated++;

                // Gentle throttle to stay under rate limits across many games.
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExpertPickAutomationService: failed to generate pick for {League} game {GameId}.", league, game.GameId);
            }
        }

        _logger.LogInformation("ExpertPickAutomationService: generation complete. {Generated} Expert Picks created/refreshed.", generated);
        return generated;
    }

    /// <summary>
    /// Enumerates in-season leagues and their scheduled (not final) games across the
    /// lookahead window. NFL is pulled by week; daily leagues are pulled per date.
    /// </summary>
    private async Task<List<(string League, NBAGameMatchup Game)>> CollectUpcomingGamesAsync(CancellationToken cancellationToken)
    {
        var results = new List<(string, NBAGameMatchup)>();
        var seen = new HashSet<string>();
        var today = NBAGameMatchup.LeagueToday;
        var windowEnd = today.AddDays(_lookaheadDays);

        // NFL — pulled by week; keep current and upcoming weeks within the window.
        try
        {
            var nflGames = await _scheduleService.GetNflGamesByWeekAsync(cancellationToken);
            foreach (var game in nflGames)
            {
                if (game.IsFinal) continue;
                if (game.GameDate < today || game.GameDate > windowEnd) continue;
                if (seen.Add($"NFL|{game.GameId}"))
                    results.Add(("NFL", game));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ExpertPickAutomationService: failed to load NFL schedule.");
        }

        // Daily-scheduled leagues — probe each date in the window.
        foreach (var league in DailyLeagues)
        {
            for (var date = today; date <= windowEnd; date = date.AddDays(1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var games = await _scheduleService.GetGamesForDateAsync(league, date, cancellationToken);
                    foreach (var game in games)
                    {
                        if (game.IsFinal) continue;
                        if (seen.Add($"{league}|{game.GameId}"))
                            results.Add((league, game));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ExpertPickAutomationService: failed to load {League} schedule for {Date}.", league, date);
                }
            }
        }

        return results;
    }

    private async Task<ExpertPick?> GeneratePickForGameAsync(string league, NBAGameMatchup game, CancellationToken cancellationToken)
    {
        var oddsLine = BuildOddsLine(game);

        var systemPrompt =
            "You are a professional sports betting analyst providing an expert pick. Analyze the matchup and return a single best bet. " +
            "Respond ONLY with a compact JSON object and nothing else, using exactly these keys: " +
            "\"pickType\" (one of \"Spread\", \"Moneyline\", \"OverUnder\"), " +
            "\"pickSelection\" (the specific bet, e.g. \"Home -3.5\", \"Away ML\", \"Over 44.5\"), " +
            "\"confidence\" (integer 1-100), " +
            "\"analysis\" (2-4 sentence rationale). " +
            "Do not wrap the JSON in code fences.";

        var userPrompt =
            $"League: {league}\n" +
            $"Matchup: {game.AwayTeamName} (away) at {game.HomeTeamName} (home)\n" +
            $"Start (local): {game.GameTimeDisplay}\n" +
            (string.IsNullOrEmpty(game.Venue) ? "" : $"Venue: {game.Venue}\n") +
            (string.IsNullOrEmpty(oddsLine) ? "" : $"Market: {oddsLine}\n") +
            "Provide your single best expert bet as JSON.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions { Temperature = 0.7f };
        var response = await _chatClient!.CompleteChatAsync(messages, options, cancellationToken);
        var raw = response.Value.Content.Count > 0 ? response.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning("ExpertPickAutomationService: empty AI response for {League} game {GameId}.", league, game.GameId);
            return null;
        }

        var parsed = ParsePick(raw);
        if (parsed == null)
        {
            _logger.LogWarning("ExpertPickAutomationService: could not parse AI response for {League} game {GameId}: {Raw}", league, game.GameId, raw);
            return null;
        }

        var gameTimeUtc = game.GameTime.Kind == DateTimeKind.Utc ? game.GameTime : game.GameTime.ToUniversalTime();

        return new ExpertPick
        {
            GameId = game.GameId,
            League = league,
            AwayTeamName = game.AwayTeamName,
            HomeTeamName = game.HomeTeamName,
            GameTime = gameTimeUtc,
            PickType = parsed.PickType,
            PickSelection = parsed.PickSelection,
            Confidence = Math.Clamp(parsed.Confidence, 1, 100),
            Analysis = parsed.Analysis,
            EnteredBy = AutomationEnteredBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task UpsertPickAsync(ExpertPick pick, CancellationToken cancellationToken)
    {
        var existing = await _db.ExpertPicks
            .FirstOrDefaultAsync(p => p.League == pick.League && p.GameId == pick.GameId, cancellationToken);

        if (existing == null)
        {
            _db.ExpertPicks.Add(pick);
        }
        else
        {
            existing.AwayTeamName = pick.AwayTeamName;
            existing.HomeTeamName = pick.HomeTeamName;
            existing.GameTime = pick.GameTime;
            existing.PickType = pick.PickType;
            existing.PickSelection = pick.PickSelection;
            existing.Confidence = pick.Confidence;
            existing.Analysis = pick.Analysis;
            existing.EnteredBy = pick.EnteredBy;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildOddsLine(NBAGameMatchup game)
    {
        var parts = new List<string>();
        if (game.Spread.HasValue) parts.Add($"Spread (home): {game.Spread.Value:+0.0;-0.0}");
        if (game.OverUnder.HasValue) parts.Add($"O/U: {game.OverUnder.Value:0.0}");
        if (game.AwayMoneyline.HasValue) parts.Add($"Away ML: {game.AwayMoneyline.Value:+0;-0}");
        if (game.HomeMoneyline.HasValue) parts.Add($"Home ML: {game.HomeMoneyline.Value:+0;-0}");
        return string.Join(", ", parts);
    }

    private ParsedPick? ParsePick(string raw)
    {
        var json = ExtractJson(raw);
        if (json == null) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var pickType = GetString(root, "pickType");
            var pickSelection = GetString(root, "pickSelection");
            if (string.IsNullOrWhiteSpace(pickType) || string.IsNullOrWhiteSpace(pickSelection))
                return null;

            return new ParsedPick
            {
                PickType = pickType!,
                PickSelection = pickSelection!,
                Confidence = GetInt(root, "confidence") ?? 50,
                Analysis = GetString(root, "analysis")
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return raw.Substring(start, end - start + 1);
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static int? GetInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)) return i;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s)) return s;
        return null;
    }

    private sealed class ParsedPick
    {
        public string PickType { get; set; } = string.Empty;
        public string PickSelection { get; set; } = string.Empty;
        public int Confidence { get; set; }
        public string? Analysis { get; set; }
    }
}
