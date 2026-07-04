<#
.SYNOPSIS
    Downloads the OVN package used by eryph-zero from the dbosoft release server
    and verifies its SHA256 checksum.

.DESCRIPTION
    Local dev runs expect an 'ovnpackage.zip' at the repository root (see
    'ovnPackagePath' in src/apps/src/Eryph-zero/appsettings.Development.json).
    This is the unsigned OVN package and is intentionally not committed to the
    repository. Run this script once to fetch it. CI fetches the same package
    the same way (see azure-pipelines.yml).

.PARAMETER Version
    The OVN release to fetch. Defaults to the version pinned by the build
    pipeline. Override for special builds.

.PARAMETER OutFile
    Target path for the package. Defaults to 'ovnpackage.zip' at the repo root.

.EXAMPLE
    ./scripts/Fetch-OvnPackage.ps1

.EXAMPLE
    ./scripts/Fetch-OvnPackage.ps1 -Version 26.3.0
#>
[CmdletBinding()]
param(
    [string]$Version = '26.3.0',
    [string]$OutFile = (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath 'ovnpackage.zip')
)

$ErrorActionPreference = 'Stop'

$indexUrl = "https://releases.dbosoft.eu/ovn/$Version/index.json"
Write-Host "Fetching OVN release index from $indexUrl"
$index = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing

$file = $index.files |
    Where-Object { $_.os -eq 'windows' -and $_.arch -eq 'amd64' } |
    Select-Object -First 1
if (-not $file) {
    throw "No windows/amd64 OVN package found in index for version $Version"
}

Write-Host "Downloading $($file.url)"
Invoke-WebRequest -Uri $file.url -OutFile $OutFile -UseBasicParsing

$expected = $file.sha256Checksum.ToLowerInvariant()
$actual = (Get-FileHash -Path $OutFile -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $expected) {
    Remove-Item -Path $OutFile -Force
    throw "OVN package hash mismatch. Expected $expected but got $actual"
}

Write-Host "OVN package verified (SHA256 $actual)"
Write-Host "Saved to $((Resolve-Path $OutFile).Path)"
