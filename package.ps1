# package.ps1 - Build the player release zip: BepInEx + the Release plugin + prism.dll +
# docs, laid out so extracting the archive into the game folder is the entire install.
# The game install is only needed for reference dlls; nothing is deployed into it, so the
# game may be running. Output: artifacts\JerianAccess-<version>.zip (version from Plugin.cs).

param(
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\package.ps1"
    Write-Host "Builds artifacts\JerianAccess-<version>.zip from a Release build."
    Write-Host "Set the JERIAN_GAME environment variable if the game is not auto-detected."
    exit 0
}

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\scripts\JerianGameLocator.ps1"

$Game = Resolve-JerianGame
if (-not (Test-JerianGameDir $Game)) {
    Write-Host "ERROR: The Life and Suffering of Prince Jerian not found at: $Game" -ForegroundColor Red
    Write-Host "Set the JERIAN_GAME environment variable to the game folder." -ForegroundColor Red
    exit 1
}

Write-Host "Packaging JerianAccess (game: $Game)..." -ForegroundColor Cyan
dotnet msbuild "$PSScriptRoot\scripts\package.proj" -t:Package "-p:GameDir=$Game"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Packaging FAILED." -ForegroundColor Red
    exit 1
}
