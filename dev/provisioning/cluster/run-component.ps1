<#
.SYNOPSIS
  Starts one split-runtime component with bus mTLS enabled, using the token / enrollment-file model.
  Matches docs/internal/cluster-mtls-bring-up.md. Run provisioning (00-provision.ps1) and mint the
  per-component enrollment files first, e.g.:
    eryph-identity new-enrollment --type Controller --fqdn <host> --endpoint https://localhost:8080/ `
      --out C:\eryph-dev-cluster\enroll\controller.json
.PARAMETER Component
  Identity | Controller | ComputeApi | Agent
.EXAMPLE
  .\run-component.ps1 Identity
  .\run-component.ps1 Controller -DbPassword eryph
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][ValidateSet('Identity','Controller','ComputeApi','Agent')]
  [string]$Component,
  [string]$OutDir = 'C:\eryph-dev-cluster',
  [string]$Broker = 'amqps://guest:guest@localhost:5671',
  [string]$DbServer = 'localhost',
  [int]$DbPort = 3306,
  [string]$DbUser = 'root',
  [string]$DbPassword = 'eryph'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$dbConn = "Server=$DbServer;Port=$DbPort;Database=eryph;User Id=$DbUser;Password=$DbPassword"
$enroll = Join-Path $OutDir 'enroll'

# Common bus mTLS settings (token / enrollment-file model). Enrolling components point at their
# operator-minted file; Identity hosts the CA and self-issues, so it has no enrollment file.
$env:componentMtls__enabled = 'true'
$env:RABBITMQ_CONNECTIONSTRING = $Broker

switch ($Component) {
  'Identity' {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'https://localhost:8080'
    $env:ERYPH_IDENTITY_BASEURL = 'https://localhost:8080/'
    $env:componentMtls__certificateDirectory = Join-Path $OutDir 'identity'
    dotnet run --project (Join-Path $repo 'src\apps\src\Eryph.Identity') -c Release --no-launch-profile
  }
  'Controller' {
    $env:ERYPH_STATEDB_CONNECTIONSTRING = $dbConn
    $env:componentMtls__enrollmentFile = Join-Path $enroll 'controller.json'
    $env:componentMtls__certificateDirectory = Join-Path $OutDir 'controller'
    # multi-targeted; the Windows TFM manages OVN like eryph-zero
    dotnet run --project (Join-Path $repo 'src\apps\src\Eryph.Controller') --framework net10.0-windows -c Release
  }
  'ComputeApi' {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'https://localhost:8443'
    $env:ERYPH_STATEDB_CONNECTIONSTRING = $dbConn
    # JWT bearer authority for the compute API's own clients — Identity now serves HTTPS, so this
    # must be HTTPS too (the in-code fallback is an http:// dev default that would no longer match).
    $env:ERYPH_IDENTITY_URL = 'https://localhost:8080/identity'
    $env:componentMtls__enrollmentFile = Join-Path $enroll 'computeapi.json'
    $env:componentMtls__certificateDirectory = Join-Path $OutDir 'apiendpoint'
    dotnet run --project (Join-Path $repo 'src\apps\src\Eryph.ApiEndpoint') -c Release --no-launch-profile
  }
  'Agent' {
    $env:componentMtls__enrollmentFile = Join-Path $enroll 'agent.json'
    $env:componentMtls__certificateDirectory = Join-Path $OutDir 'agent'
    dotnet run --project (Join-Path $repo 'src\apps\src\Eryph.Agent') -c Release
  }
}
