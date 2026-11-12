# Downloads a Godot archive, unzips it and prints the full path of the Godot executable.
#
# A failed download or unzip (a gateway error such as 504, a connection error, a truncated archive) is retried, up to
# -MaxAttempts attempts in all, waiting DelaySeconds * attempt seconds after each failed attempt but the last. Every
# failed attempt writes a warning, so the retry shows in the run log. An archive that unzips but lacks the executable
# fails at once, because another download cannot fix it. Test-InstallGodot.ps1 proves the loop against a local server.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Uri,

    [Parameter(Mandatory)]
    [string]$ArchivePath,

    [Parameter(Mandatory)]
    [string]$ExtractPath,

    # File name of the Godot executable that must exist under ExtractPath after the unzip.
    [Parameter(Mandatory)]
    [string]$BinaryName,

    [Parameter()]
    [ValidateRange(1, 10)]
    [int]$MaxAttempts = 3,

    [Parameter()]
    [ValidateRange(0, 300)]
    [int]$DelaySeconds = 15,

    # Seconds allowed for one download before it counts as a failed attempt.
    [Parameter()]
    [ValidateRange(1, 3600)]
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 draws a progress bar that slows a large download a lot; pwsh 7 does not need it either.
$ProgressPreference = 'SilentlyContinue'

$lastError = $null
$succeeded = $false
for ($attempt = 1; $attempt -le $MaxAttempts -and -not $succeeded; $attempt++) {
    try {
        if (Test-Path -LiteralPath $ArchivePath) {
            Remove-Item -LiteralPath $ArchivePath -Force
        }
        Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile $ArchivePath -TimeoutSec $TimeoutSeconds
        Expand-Archive -LiteralPath $ArchivePath -DestinationPath $ExtractPath -Force
        $succeeded = $true
    }
    catch {
        $lastError = $_.Exception.Message
        Write-Warning "Godot download attempt $attempt of $MaxAttempts failed: $lastError"
        if ($attempt -lt $MaxAttempts) {
            Start-Sleep -Seconds ($DelaySeconds * $attempt)
        }
    }
}

if (-not $succeeded) {
    throw "Godot download failed after $MaxAttempts attempts: $lastError"
}

$binary = Get-ChildItem -LiteralPath $ExtractPath -Filter $BinaryName -File -Recurse | Select-Object -First 1
if ($null -eq $binary) {
    throw "Godot binary was not found under $ExtractPath"
}

Write-Output $binary.FullName
