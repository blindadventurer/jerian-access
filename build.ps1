# build.ps1 - Build the mod and deploy it into the game folder: host + Core +
# Mono.CSharp + lang/ under BepInEx\plugins\JerianAccess\, the reloadable module under
# module\, and prism.dll beside the game exe. The deploy itself is done by the csproj
# Debug targets (DeployMod / DeployModule); this script resolves the game install,
# checks preconditions, and runs the build with GameDir pointed at it.

param(
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\build.ps1"
    Write-Host "Builds the solution and deploys the mod into the detected game install."
    Write-Host "Set the JERIAN_GAME environment variable if the game is not auto-detected."
    exit 0
}

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\scripts\JerianGameLocator.ps1"

# --- Locate the game install (same resolution as setup-bepinex.ps1) ---
$Game = Resolve-JerianGame
if (-not (Test-JerianGameDir $Game)) {
    Write-Host "ERROR: The Life and Suffering of Prince Jerian not found at: $Game" -ForegroundColor Red
    Write-Host "Set the JERIAN_GAME environment variable to the game folder." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path "$Game\BepInEx\core\BepInEx.dll")) {
    Write-Host "ERROR: BepInEx is not installed at $Game\BepInEx." -ForegroundColor Red
    Write-Host "Run setup-bepinex.ps1 first." -ForegroundColor Red
    exit 1
}

# The host and Core dlls are loaded from disk and locked while the game runs; a deploy
# over them fails and leaves a stale install. (Module-only rebuilds with the game
# running are the hot-reload path: dotnet build src\JerianAccess.Module\JerianAccess.Module.csproj)
if (Test-JerianGameRunning) {
    Write-Host "ERROR: the game is running. Close it, then re-run this script." -ForegroundColor Red
    exit 1
}

# --- Build + deploy (csproj Debug targets copy everything into $Game) ---
Write-Host "Building JerianAccess (game: $Game)..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot\JerianAccess.slnx" -c Debug "-p:GameDir=$Game"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}

# --- Verify the deploy landed ---
$Expected = @(
    "$Game\BepInEx\plugins\JerianAccess\JerianAccess.dll",
    "$Game\BepInEx\plugins\JerianAccess\JerianAccess.Core.dll",
    "$Game\BepInEx\plugins\JerianAccess\module\JerianAccess.Module.dll",
    "$Game\BepInEx\plugins\JerianAccess\lang\en\ui.txt",
    "$Game\prism.dll"
)
$Missing = $Expected | Where-Object { -not (Test-Path $_) }
if ($Missing) {
    Write-Host "ERROR: build succeeded but these deployed files are missing:" -ForegroundColor Red
    $Missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host ""
Write-Host "Done. Launch the game and listen for the startup line." -ForegroundColor Cyan
