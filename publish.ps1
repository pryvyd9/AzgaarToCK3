param([switch]$LocalOnly)

$ErrorActionPreference = "Stop"

# --- Prerequisites ---
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "ERROR: 'dotnet' not found on PATH. Install .NET 10 SDK."
    exit 1
}
if (-not $LocalOnly) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Error "ERROR: 'gh' (GitHub CLI) not found. Install from https://cli.github.com"
        exit 1
    }
    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ERROR: Not logged into GitHub CLI. Run 'gh auth login' first."
        exit 1
    }
}

# --- Extract version from changelog.md ---
$changelogPath = Join-Path $PSScriptRoot "changelog.md"
$changelogLines = Get-Content $changelogPath
$version = $null
foreach ($line in $changelogLines) {
    if ($line -match '^#\s*(\d+\.\d+\.\d+)\s*$') {
        $version = $Matches[1]
        break
    }
}
if (-not $version) {
    Write-Error "ERROR: Could not find version heading in changelog.md (expected '# X.Y.Z')."
    exit 1
}
Write-Host "Publishing version $version"

if (-not $LocalOnly) {
    # --- Safety: existing git tag ---
    $existingTag = git tag -l $version
    if ($existingTag) {
        Write-Error "ERROR: Git tag '$version' already exists. Bump the version in changelog.md before publishing."
        exit 1
    }

    # --- Safety: existing GitHub release ---
    gh release view $version 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Error "ERROR: GitHub release '$version' already exists."
        exit 1
    }

    # --- Create and push git tag ---
    Write-Host "Creating tag $version..."
    git tag $version
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed to create git tag."; exit 1 }
    git push origin $version
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed to push git tag."; exit 1 }
}

# --- Build output directory ---
$outDir = Join-Path $PSScriptRoot "publish_tmp"
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
$proj = Join-Path $PSScriptRoot "ConsoleUI/ConsoleUI.csproj"
$commonArgs = @(
    '-c', 'Release',
    '/p:DebugType=None',
    '/p:DebugSymbols=false',
    '/p:CopyOutputSymbolsToPublishDirectory=false',
    '--self-contained', 'true'
)

# --- dotnet publish ---
Write-Host "Publishing win-x64..."
dotnet publish $proj -r win-x64 @commonArgs `
    /p:PublishAot=true `
    /p:PublishReadyToRun=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o (Join-Path $outDir "win-x64")
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Publishing osx-x64..."
dotnet publish $proj -r osx-x64 @commonArgs `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o (Join-Path $outDir "osx-x64")
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Publishing linux-x64..."
dotnet publish $proj -r linux-x64 @commonArgs `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o (Join-Path $outDir "linux-x64")
if ($LASTEXITCODE -ne 0) { exit 1 }

# --- Create ZIP archives ---
$readmePath = Join-Path $PSScriptRoot "Readme.md"
$zips = @()
foreach ($rid in @('win-x64', 'osx-x64', 'linux-x64')) {
    $zip = Join-Path $PSScriptRoot "AzgaarToCK3_${version}_${rid}.zip"
    Write-Host "Creating $zip..."
    Compress-Archive -Path (Join-Path $outDir "$rid/*") -DestinationPath $zip -Force
    Compress-Archive -Path $readmePath -DestinationPath $zip -Update
    $zips += $zip
}

if (-not $LocalOnly) {
    # --- Extract release notes ---
    $inSection = $false
    $noteLines = @()
    foreach ($line in $changelogLines) {
        if ($line -match "^#\s*$([regex]::Escape($version))\s*$") {
            $inSection = $true
            continue
        }
        if ($inSection -and $line -match '^#\s*\d') {
            break
        }
        if ($inSection) {
            $noteLines += $line
        }
    }
    $notes = ($noteLines -join "`n").Trim()

    # --- Create GitHub release ---
    Write-Host "Creating GitHub release $version..."
    gh release create $version @zips --title "AzgaarToCK3 $version" --notes $notes
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed to create GitHub release."; exit 1 }
}

# --- Cleanup ---
Remove-Item $outDir -Recurse -Force

if ($LocalOnly) {
    Write-Host "Done. ZIPs created locally (no GitHub release):"
    $zips | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "Done. Released $version to GitHub."
}
