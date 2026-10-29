<#
Writes the SHA-256 hash of every file of an Obenseuer install to a JSON list, for verify-game.

  hash-game.bat [game folder] [list.json] [-Version 0.5.13] [-Build 26179069] [-Exclude 'Logs/*','*.tmp']

The game folder defaults to %OBENSEUER_DIR%, else the current folder. The list defaults to
hashes\<version>.json next to this script. What ObenseuerExtended adds (Mods, ObenLoader, the loader's DLLs)
and store-specific files (Steam, GOG, Epic integration) are left out; files the installer changed are hashed
from the original it kept (*.obenbak). Paths in the list and in -Exclude use forward slashes.
#>
param(
    [string]$Game = $env:OBENSEUER_DIR,
    [string]$Out,
    [string]$Version = "unknown",
    [string]$Build,
    [string[]]$Exclude = @()
)
$ErrorActionPreference = "Stop"

if (-not $Game) { $Game = (Get-Location).Path }
if (-not (Test-Path -LiteralPath (Join-Path $Game "Obenseuer_Data"))) {
    Write-Host "hash-game: $Game isn't an Obenseuer folder (no Obenseuer_Data in it). Pass the game folder."
    exit 1
}
$Game = (Resolve-Path -LiteralPath $Game).Path.TrimEnd('\', '/')
if (-not $Out) { $Out = Join-Path $PSScriptRoot "hashes\$Version.json" }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Out) | Out-Null

# The same rules as hash-game.sh; store integration differs between Steam, GOG and Epic copies of one version.
$skip = @(
    'Mods/*', 'ObenLoader/*', 'BepInEx/*', '*.obenbak',
    'Obenseuer_Data/Managed/ObenLoader.*', 'Obenseuer_Data/Managed/0Harmony.dll', 'Obenseuer_Data/Managed/Mono.Cecil*.dll',
    '*steam_api*', '*steam_appid.txt', '*/steam_settings/*', '*/Galaxy*.dll', '*goggame-*', '*/EOSSDK*'
) + $Exclude

Write-Host "Hashing $Game ..."
$files = @(Get-ChildItem -LiteralPath $Game -Recurse -File | ForEach-Object {
    $_.FullName.Substring($Game.Length).TrimStart('\', '/').Replace('\', '/')
})
[Array]::Sort($files, [StringComparer]::Ordinal)

$entries = New-Object System.Collections.Generic.List[string]
foreach ($file in $files) {
    $skipped = $false
    foreach ($pattern in $skip) {
        if ($file -like $pattern) { $skipped = $true; break }
    }
    if ($skipped) { continue }
    $source = Join-Path $Game $file
    # The installer keeps the original of each file it changes; that is the game's file.
    if (Test-Path -LiteralPath "$source.obenbak") { $source = "$source.obenbak" }
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    $escaped = $file.Replace('\', '\\').Replace('"', '\"')
    $entries.Add("    `"$escaped`": `"$hash`"")
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("{")
$lines.Add("  `"game`": `"Obenseuer`",")
$lines.Add("  `"version`": `"$Version`",")
if ($Build) { $lines.Add("  `"steamBuild`": `"$Build`",") }
$lines.Add("  `"files`": {")
for ($i = 0; $i -lt $entries.Count; $i++) {
    $lines.Add($entries[$i] + $(if ($i -lt $entries.Count - 1) { "," } else { "" }))
}
$lines.Add("  }")
$lines.Add("}")
# LF line ends and no byte order mark, so verify-game.sh reads lists made on Windows too.
[IO.File]::WriteAllText($Out, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding $false))
Write-Host "Wrote $($entries.Count) files to $Out"
