<#
Checks an Obenseuer install against a hash list from hash-game and lists every file that is changed or
missing. Handy when the game or a mod misbehaves and you want to rule out a broken install.

  verify-game.bat [game folder] [list.json]

The game folder defaults to %OBENSEUER_DIR%, else the current folder. Without a list, the one in hashes\ next
to this script whose Assembly-CSharp.dll matches the game is used. Files ObenseuerExtended adds are never in
the lists; files its installer changed are checked through the original it kept (*.obenbak). Exits with 0 when
everything matches, 1 when something doesn't, 2 when it can't check.
#>
param(
    [string]$Game = $env:OBENSEUER_DIR,
    [string]$List
)
$ErrorActionPreference = "Stop"

if (-not $Game) { $Game = (Get-Location).Path }
if (-not (Test-Path -LiteralPath (Join-Path $Game "Obenseuer_Data"))) {
    Write-Host "verify-game: $Game isn't an Obenseuer folder (no Obenseuer_Data in it). Pass the game folder."
    exit 2
}
$Game = (Resolve-Path -LiteralPath $Game).Path

function Get-Sha([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$code = "Obenseuer_Data/Managed/Assembly-CSharp.dll"
if (-not $List) {
    $mine = Get-Sha (Join-Path $Game $code)
    foreach ($candidate in Get-ChildItem -Path (Join-Path $PSScriptRoot "hashes") -Filter *.json -ErrorAction SilentlyContinue) {
        $data = Get-Content -Raw -LiteralPath $candidate.FullName | ConvertFrom-Json
        $entry = $data.files.PSObject.Properties[$code]
        if ($entry -and $entry.Value -eq $mine) { $List = $candidate.FullName; break }
    }
    if (-not $List) {
        Write-Host "verify-game: no list in $(Join-Path $PSScriptRoot 'hashes') matches this game's code (Assembly-CSharp.dll $($mine.Substring(0, 12))). Its version may be newer than these lists; pass a list made with hash-game."
        exit 2
    }
}
if (-not (Test-Path -LiteralPath $List)) { Write-Host "verify-game: no list at $List"; exit 2 }

$data = Get-Content -Raw -LiteralPath $List | ConvertFrom-Json
$version = if ($data.version) { $data.version } else { "unknown" }
Write-Host "Checking $Game against $(Split-Path -Leaf $List) (game version $version)..."
$total = 0
$problems = 0
foreach ($entry in $data.files.PSObject.Properties) {
    $total++
    $file = Join-Path $Game $entry.Name
    if (Test-Path -LiteralPath "$file.obenbak") { $file = "$file.obenbak" }
    if (-not (Test-Path -LiteralPath $file)) {
        Write-Host "missing  $($entry.Name)"
        $problems++
    } elseif ((Get-Sha $file) -ne $entry.Value) {
        Write-Host "changed  $($entry.Name)"
        $problems++
    }
}

if ($problems -eq 0) {
    Write-Host "All $total files match."
    exit 0
}
Write-Host "$problems of $total files don't match. Verify the game's files in your store (Steam: Properties > Installed Files > Verify) or reinstall it."
exit 1
