<#
.SYNOPSIS
    Build wl from a commit (default: the checkout's HEAD) and install it into a versioned local directory. Windows only.

.DESCRIPTION
    Builds the requested commit in a temporary git worktree (your checkout is not touched),
    runs the unit and E2E tests against the native binary, copies it to
    <InstallRoot>\<version>\, and points the <InstallRoot>\current junction at it.
    <InstallRoot>\current is added to the user PATH once (unless -NoPath); upgrades and
    rollbacks only move the junction.

    Works in Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    .\scripts\install-local.ps1                 # checkout's HEAD (e.g. latest master after 'git pull')
.EXAMPLE
    .\scripts\install-local.ps1 -Ref v0.9.0     # a release tag
.EXAMPLE
    .\scripts\install-local.ps1 -Use 0.8.0      # switch/roll back to an installed version
.EXAMPLE
    .\scripts\install-local.ps1 -List
#>
#Requires -Version 5.1
[CmdletBinding(DefaultParameterSetName = 'Install')]
param(
    # Git tag, branch or commit to build. Defaults to HEAD of this checkout.
    [Parameter(ParameterSetName = 'Install')] [string] $Ref,
    # Skip unit and E2E tests (not recommended).
    [Parameter(ParameterSetName = 'Install')] [switch] $SkipTests,
    # Replace an existing install of the same version.
    [Parameter(ParameterSetName = 'Install')] [switch] $Force,
    # Install without switching 'current' to the new version.
    [Parameter(ParameterSetName = 'Install')] [switch] $NoSwitch,
    # Switch 'current' to an already installed version.
    [Parameter(ParameterSetName = 'Use', Mandatory)] [string] $Use,
    # List installed versions.
    [Parameter(ParameterSetName = 'List', Mandatory)] [switch] $List,
    # Where versions are installed.
    [string] $InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\wl'),
    # Do not modify the user PATH.
    [switch] $NoPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Windows PowerShell 5.1 (Desktop) is Windows-only and has no $IsWindows.
if ($PSVersionTable.PSEdition -ne 'Desktop' -and -not $IsWindows) {
    throw 'install-local.ps1 supports Windows only. On other platforms, follow "Build from source" in the README.'
}

$exeName = 'wl.exe'
$currentLink = Join-Path $InstallRoot 'current'

function Get-InstalledVersions {
    if (-not (Test-Path $InstallRoot)) { return @() }
    Get-ChildItem $InstallRoot -Directory |
        Where-Object { $_.Name -ne 'current' -and (Test-Path (Join-Path $_.FullName $exeName)) } |
        Select-Object -ExpandProperty Name
}

function Get-CurrentTarget {
    $item = Get-Item $currentLink -ErrorAction SilentlyContinue
    if ($item -and $item.LinkType) { return (@($item.Target) | Select-Object -First 1) }
    return $null
}

function Set-Current([string] $version) {
    $target = Join-Path $InstallRoot $version
    if (-not (Test-Path (Join-Path $target $exeName))) { throw "Version '$version' is not installed in $InstallRoot." }
    $existing = Get-Item $currentLink -ErrorAction SilentlyContinue
    if ($existing) {
        if (-not $existing.LinkType) { throw "$currentLink exists and is not a junction; remove it manually." }
        # Delete removes only the junction, never the target directory.
        $existing.Delete()
    }
    New-Item -ItemType Junction -Path $currentLink -Target $target | Out-Null
    Write-Host "current -> $target"
}

function Confirm-OnPath {
    if (-not $NoPath) {
        $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
        if ((($userPath -split ';') | Where-Object { $_ }) -notcontains $currentLink) {
            [Environment]::SetEnvironmentVariable('PATH', ($userPath.TrimEnd(';') + ";$currentLink"), 'User')
            Write-Host "Added $currentLink to the user PATH (open a new terminal to pick it up)."
        }
    }

    $first = Get-Command wl -All -CommandType Application, ExternalScript -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Source -First 1
    if ($first -and -not $first.StartsWith($currentLink, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Warning "'wl' currently resolves to $first, which takes precedence over $currentLink. Remove that installation (e.g. 'npm uninstall -g @ctx-launcher/wl') or reorder PATH."
    }
}

switch ($PSCmdlet.ParameterSetName) {
    'List' {
        $active = Get-CurrentTarget
        foreach ($v in Get-InstalledVersions) {
            $marker = if ($active -and (Split-Path $active -Leaf) -eq $v) { '*' } else { ' ' }
            Write-Host "$marker $v"
        }
        return
    }
    'Use' {
        Set-Current $Use
        Confirm-OnPath
        return
    }
}

# ---- Install ----
foreach ($tool in 'git', 'dotnet') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "'$tool' is required on PATH." }
}

$repo = & git -C $PSScriptRoot rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0 -or -not $repo) { throw 'Run this script from a ctx-launcher git checkout.' }
$repo = $repo.Trim()

if (-not $Ref) {
    $Ref = 'HEAD'
    if (& git -C $repo status --porcelain --untracked-files=no) {
        Write-Warning 'Your checkout has uncommitted changes; they are not included (the build uses a clean worktree of HEAD).'
    }
}
$commit = & git -C $repo rev-parse --verify --quiet "$Ref^{commit}"
if ($LASTEXITCODE -ne 0 -or -not $commit) { throw "Unknown ref '$Ref'. Fetch it first ('git fetch --tags') or pass a valid tag/commit." }
$commit = $commit.Trim()
Write-Host "Building $Ref ($commit)"

# Exact release tags map to a known version (MinVer), so an existing install can be
# detected before spending time on a build. Other refs are checked after publishing.
if (-not $Force -and $Ref -match '^v(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)$') {
    $expected = Join-Path $InstallRoot $Matches[1]
    if (Test-Path $expected) { throw "Version $($Matches[1]) is already installed at $expected. Use -Force to replace it, or -Use $($Matches[1]) to switch to it." }
}

if (-not (Get-Command vswhere -ErrorAction SilentlyContinue)) {
    # Native AOT locates MSVC via vswhere.exe but does not add its folder to PATH.
    $installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    if (-not (Test-Path (Join-Path $installer 'vswhere.exe'))) { throw 'MSVC C++ build tools (Visual Studio or Build Tools) are required for native AOT publishing.' }
    $env:PATH = "$installer;$env:PATH"
}

$worktree = Join-Path ([IO.Path]::GetTempPath()) ('wl-build-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
& git -C $repo worktree add --detach --quiet $worktree $commit
if ($LASTEXITCODE -ne 0) { throw 'git worktree add failed.' }
try {
    Push-Location $worktree
    function Invoke-Step([string] $label, [scriptblock] $block) {
        Write-Host "==> $label"
        & $block
        if ($LASTEXITCODE -ne 0) { throw "$label failed (exit $LASTEXITCODE)." }
    }

    if (-not $SkipTests) {
        Invoke-Step 'Unit tests' { dotnet test 'tests\wl.tests\wl.tests.csproj' --verbosity quiet }
    }
    Invoke-Step 'Publish (win-x64)' { dotnet publish 'src\wl\wl.csproj' -c Release -r win-x64 --verbosity quiet }
    $binary = Join-Path $worktree "src\wl\bin\Release\net10.0\win-x64\publish\$exeName"
    if (-not (Test-Path $binary)) { throw "Publish did not produce $binary." }
    if (-not $SkipTests) {
        $env:WL_BINARY_PATH = $binary
        Invoke-Step 'E2E tests' { dotnet test 'tests\wl.e2e.tests\wl.e2e.tests.csproj' --verbosity quiet }
        Remove-Item Env:\WL_BINARY_PATH
    }

    $version = ((& $binary --version) -split '\+')[0].Trim()
    if (-not $version) { throw 'Could not read the version from the built binary.' }

    $dest = Join-Path $InstallRoot $version
    if (Test-Path $dest) {
        if (-not $Force) { throw "Version $version is already installed at $dest. Use -Force to replace it, or -Use $version to switch to it." }
        Remove-Item $dest -Recurse -Force
    }
    New-Item -ItemType Directory $dest -Force | Out-Null
    Copy-Item $binary $dest
    Write-Host "Installed $version to $dest"
}
finally {
    Pop-Location
    # Best effort; in Windows PowerShell 5.1 redirected native stderr would throw under 'Stop'.
    $ErrorActionPreference = 'Continue'
    & git -C $repo worktree remove --force $worktree *> $null
    $ErrorActionPreference = 'Stop'
}

if (-not $NoSwitch) {
    Set-Current $version
    Confirm-OnPath
    Write-Host "Bundled skills refresh automatically on first wl launch/create/clone."
}
