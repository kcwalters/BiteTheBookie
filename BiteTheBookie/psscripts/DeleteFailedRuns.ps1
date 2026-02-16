# Set your repository details
$owner = "kcwalters"
$repo = "BiteTheBookie"

# You'll need a GitHub Personal Access Token with 'repo' scope
# Create one at: https://github.com/settings/tokens
$token = "YOUR_GITHUB_TOKEN_HERE"

# Set up headers
$headers = @{
    "Accept" = "application/vnd.github+json"
    "Authorization" = "Bearer $token"
}

# Get all failed workflow runs
$page = 1
$allRuns = @()

do {
    $url = "https://api.github.com/repos/$owner/$repo/actions/runs?status=failure&per_page=100&page=$page"
    $response = Invoke-RestMethod -Uri $url -Headers $headers -Method Get
    $allRuns += $response.workflow_runs
    $page++
} while ($response.workflow_runs.Count -eq 100)

Write-Host "Found $($allRuns.Count) failed workflow runs"

# Delete each failed run
$count = 0
foreach ($run in $allRuns) {
    $count++
    Write-Host "Deleting run $count of $($allRuns.Count): $($run.id) - $($run.name)"
    
    try {
        $deleteUrl = "https://api.github.com/repos/$owner/$repo/actions/runs/$($run.id)"
        Invoke-RestMethod -Uri $deleteUrl -Headers $headers -Method Delete
        Write-Host "  ✓ Deleted successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "  ✗ Failed to delete: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Add a small delay to avoid rate limiting
    Start-Sleep -Milliseconds 500
}

Write-Host "`nDone! Deleted $count workflow runs." -ForegroundColor Cyan