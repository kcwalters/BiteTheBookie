using BiteTheBookie.Data;
using BiteTheBookie.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.ViewComponents;

/// <summary>
/// Renders admin-authored HTML content blocks for a given page and region, optionally
/// filtered by sport/league and/or team. Blocks with a blank Sport/Team match any value,
/// and blocks targeting page "*" render on every page.
/// </summary>
public class ContentRegionViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;

    public ContentRegionViewComponent(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync(
        string page,
        string region,
        string? sport = null,
        string? team = null)
    {
        page = (page ?? string.Empty).Trim();
        region = (region ?? string.Empty).Trim();

        var query = _db.ContentBlocks
            .AsNoTracking()
            .Where(b => b.IsPublished
                        && b.RegionKey == region
                        && (b.PageKey == page || b.PageKey == "*"));

        if (!string.IsNullOrWhiteSpace(sport))
        {
            var sportValue = sport.Trim();
            query = query.Where(b => b.Sport == null || b.Sport == "" || b.Sport == sportValue);
        }
        else
        {
            query = query.Where(b => b.Sport == null || b.Sport == "");
        }

        if (!string.IsNullOrWhiteSpace(team))
        {
            var teamValue = team.Trim();
            query = query.Where(b => b.Team == null || b.Team == "" || b.Team == teamValue);
        }
        else
        {
            query = query.Where(b => b.Team == null || b.Team == "");
        }

        var blocks = await query
            .OrderBy(b => b.SortOrder)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(blocks);
    }
}
