param(
    [string]$OutputFile = ".solve-session/dead-code-analysis.txt"
)

$ErrorActionPreference = "Continue"
$ProjectDir = Split-Path -Parent $PSScriptRoot

Write-Host "Running dead code analysis..."
Write-Host "Project: $ProjectDir"
Write-Host "Output: $OutputFile"
Write-Host ""

Push-Location $ProjectDir

try {
    # Run build and capture all output (don't use /warnaserror to avoid build failures)
    dotnet build --no-incremental 2>&1 | Tee-Object -FilePath $OutputFile

    Write-Host ""
    Write-Host "Filtering results..."

    # Filter for dead code warnings, excluding addons/ and Tests/
    $filtered = Get-Content $OutputFile -ErrorAction SilentlyContinue |
        Where-Object { $_ -match "IDE0051|IDE0052|CA1822" } |
        Where-Object { $_ -notmatch "addons/" } |
        Where-Object { $_ -notmatch "Tests/" } |
        Sort-Object -Unique

    if ($filtered) {
        $filtered | Out-File "${OutputFile}.filtered"

        $unusedCount = ($filtered | Where-Object { $_ -match "IDE0051" }).Count
        $unreadCount = ($filtered | Where-Object { $_ -match "IDE0052" }).Count
        $staticCount = ($filtered | Where-Object { $_ -match "CA1822" }).Count
    } else {
        $unusedCount = 0
        $unreadCount = 0
        $staticCount = 0
        "" | Out-File "${OutputFile}.filtered"
    }

    Write-Host "Results:"
    Write-Host "  IDE0051 (unused private members): $unusedCount"
    Write-Host "  IDE0052 (unread private members): $unreadCount"
    Write-Host "  CA1822 (could be static): $staticCount"
    Write-Host ""
    Write-Host "Filtered output saved to: ${OutputFile}.filtered"
    Write-Host "Full output saved to: $OutputFile"
    Write-Host "Review filtered results to identify dead code candidates."
} finally {
    Pop-Location
}
