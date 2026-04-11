# Contributing to BiteTheBookie

## Theme & Styling Standards

Every page **must** render inside the shared layout and use the centralised stylesheet.
Following these rules ensures visual consistency across the entire site.

### Layout

| Rule | Detail |
|---|---|
| Shared layout | `Views/_ViewStart.cshtml` assigns `_Layout` globally — **never** set `Layout` in individual views |
| Identity pages | `Areas/Identity/Pages/_ViewStart.cshtml` points to `/Views/Shared/_Layout.cshtml` |
| Razor Pages | Any new `Pages/_ViewStart.cshtml` must also point to `/Views/Shared/_Layout.cshtml` |

### CSS

| Rule | Detail |
|---|---|
| Single theme file | All shared styles live in `wwwroot/css/site-theme.css`, loaded after `style.css` in the layout |
| No inline `<style>` blocks | Do **not** add `<style>` sections in `.cshtml` views — add rules to `site-theme.css` instead |
| CSS custom properties | Use the `--btb-*` variables defined in `:root` of `site-theme.css` (e.g. `--btb-primary`, `--btb-body`, `--btb-muted`) |
| Component classes | Reuse existing classes: `.content-card`, `.score-badge`, `.section-header`, `.markdown-content`, `.pricing-card` |

### Icons

All icons use **Font Awesome 6** (`fas fa-*`). Do **not** use Bootstrap Icons (`bi bi-*`) — the BI stylesheet is not loaded.

### New View Checklist

When adding a new page, verify:

1. **No `@{ Layout = "..."; }` override** — rely on `_ViewStart.cshtml`
2. **No `<style>` block** — put any new rules in `site-theme.css`
3. **Uses `--btb-*` variables** for brand colours, not hardcoded hex values
4. **Uses `.content-card`** for card containers (not custom card wrappers)
5. **Uses `fas fa-*`** for icons
6. **Wraps content in `<div class="container py-4">`** for consistent spacing
7. **Sets `ViewData["Title"]`** for the browser tab title
8. **Sections are optional** — `Styles` and `Scripts` sections are available but not required

### Example Skeleton

```razor
@model MyViewModel

@{
    ViewData["Title"] = "Page Title";
}

<div class="container py-4">
    <div class="content-card mb-4">
        <h1 class="h2 mb-0">Page Heading</h1>
    </div>

    <div class="content-card">
        <!-- page content using site-theme.css classes -->
    </div>
</div>

@section Scripts {
    <script src="~/js/my-page.js" asp-append-version="true"></script>
}
```

### Ticker Row (optional)

If the page should display the live sport tickers, include the standard ticker block **before** the main content container. See `Views/Picks/Index.cshtml` for the canonical example.