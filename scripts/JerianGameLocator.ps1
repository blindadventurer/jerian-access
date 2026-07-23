# JerianGameLocator.ps1 - Shared game-install resolution for setup-bepinex.ps1 and
# build.ps1. Dot-source this file, then call Resolve-JerianGame / Test-JerianGameDir.
# Resolution order: JERIAN_GAME env var, then Steam (registry roots + every library in
# libraryfolders.vdf), then GOG (uninstall registry entries + default Galaxy paths).

$JerianGameFolderName = "The Life and Suffering of Prince Jerian"
$JerianExeName = "The Life and Suffering of Prince Jerian.exe"
$JerianManagedRelPath = "The Life and Suffering of Prince Jerian_Data\Managed\Assembly-CSharp.dll"
$JerianDefaultSteamPath = "C:\Program Files (x86)\Steam\steamapps\common\$JerianGameFolderName"
# Non-store installs checked before Steam/GOG detection (this machine keeps the game here).
$JerianKnownLocalPaths = @("F:\games\$JerianGameFolderName")

function Normalize-JerianPath {
    param([string]$Path)

    if (-not $Path) { return $null }
    $normalized = $Path.Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $null }
    return $normalized
}

function Test-JerianGameDir {
    param([string]$Path)

    $game = Normalize-JerianPath $Path
    if (-not $game) { return $false }

    return (Test-Path (Join-Path $game $JerianExeName)) -and
        (Test-Path (Join-Path $game $JerianManagedRelPath))
}

function Get-SteamLibraryPathsFromVdf {
    param([string]$Content)

    if (-not $Content) { return @() }

    $paths = @()
    [regex]::Matches($Content, '"path"\s+"([^"]+)"') | ForEach-Object {
        $paths += ($_.Groups[1].Value -replace '\\\\', '\')
    }
    return $paths
}

function Get-SteamRoots {
    $roots = @()

    $hkcuSteam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if ($hkcuSteam) { $roots += ($hkcuSteam -replace '/', '\') }

    $hklmSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    if ($hklmSteam) { $roots += $hklmSteam }

    $roots += "C:\Program Files (x86)\Steam"

    $seen = @{}
    foreach ($root in $roots) {
        $normalized = Normalize-JerianPath $root
        if (-not $normalized) { continue }
        $key = $normalized.ToLowerInvariant()
        if (-not $seen.ContainsKey($key)) {
            $seen[$key] = $true
            $normalized
        }
    }
}

function Get-SteamJerianCandidates {
    param([string[]]$SteamRoots)

    $candidates = @()
    foreach ($steam in $SteamRoots) {
        $root = Normalize-JerianPath $steam
        if (-not $root) { continue }

        $candidates += (Join-Path $root "steamapps\common\$JerianGameFolderName")

        $libraryFolders = Join-Path $root "steamapps\libraryfolders.vdf"
        if (Test-Path $libraryFolders) {
            $content = Get-Content $libraryFolders -Raw
            foreach ($library in Get-SteamLibraryPathsFromVdf $content) {
                $candidates += (Join-Path $library "steamapps\common\$JerianGameFolderName")
            }
        }
    }

    return $candidates
}

function Get-GogInstallPathsFromRegistry {
    $candidates = @()
    $roots = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKCU:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }

        Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
            $app = Get-ItemProperty -Path $_.PSPath -ErrorAction SilentlyContinue
            if (-not $app) { return }

            $displayName = [string]$app.DisplayName
            if (-not $displayName -or -not $displayName.ToLowerInvariant().Contains("jerian")) {
                return
            }

            if ($app.InstallLocation) { $candidates += $app.InstallLocation }
            if ($app.InstallDir) { $candidates += $app.InstallDir }
        }
    }

    return $candidates
}

function Get-GogJerianCandidates {
    param([string[]]$GogInstallPaths)

    $candidates = @()
    if ($GogInstallPaths) {
        $candidates += $GogInstallPaths
    } else {
        $candidates += Get-GogInstallPathsFromRegistry
    }

    $candidates += "C:\GOG Games\$JerianGameFolderName"
    $candidates += "C:\Program Files (x86)\GOG Galaxy\Games\$JerianGameFolderName"
    $candidates += "C:\Program Files\GOG Galaxy\Games\$JerianGameFolderName"

    return $candidates
}

function Add-JerianCandidate {
    param(
        [System.Collections.ArrayList]$Candidates,
        [hashtable]$Seen,
        [string]$Path
    )

    $normalized = Normalize-JerianPath $Path
    if (-not $normalized) { return }

    $key = $normalized.ToLowerInvariant()
    if ($Seen.ContainsKey($key)) { return }

    $Seen[$key] = $true
    [void]$Candidates.Add($normalized)
}

function Get-JerianGameCandidates {
    param(
        [string]$ManualGame = $env:JERIAN_GAME,
        [string[]]$SteamRoots = $(Get-SteamRoots),
        [string[]]$GogInstallPaths = $null
    )

    $candidates = New-Object System.Collections.ArrayList
    $seen = @{}

    Add-JerianCandidate $candidates $seen $ManualGame

    foreach ($candidate in $JerianKnownLocalPaths) {
        Add-JerianCandidate $candidates $seen $candidate
    }

    foreach ($candidate in Get-SteamJerianCandidates $SteamRoots) {
        Add-JerianCandidate $candidates $seen $candidate
    }

    foreach ($candidate in Get-GogJerianCandidates $GogInstallPaths) {
        Add-JerianCandidate $candidates $seen $candidate
    }

    return @($candidates)
}

function Resolve-JerianGame {
    param(
        [string]$ManualGame = $env:JERIAN_GAME,
        [string[]]$SteamRoots = $(Get-SteamRoots),
        [string[]]$GogInstallPaths = $null
    )

    $manual = Normalize-JerianPath $ManualGame
    if ($manual) {
        return $manual
    }

    foreach ($candidate in Get-JerianGameCandidates -ManualGame $null -SteamRoots $SteamRoots -GogInstallPaths $GogInstallPaths) {
        if (Test-JerianGameDir $candidate) {
            return $candidate
        }
    }

    return $JerianDefaultSteamPath
}

function Test-JerianGameRunning {
    return $null -ne (Get-Process -Name "The Life and Suffering of Prince Jerian" -ErrorAction SilentlyContinue)
}
