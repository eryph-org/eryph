<#
.SYNOPSIS
  Starts one split-runtime component with bus mTLS enabled. Reference launcher matching
  docs/internal/cluster-mtls-bring-up.md. Run provisioning (00-provision.ps1) first.
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
  [string]$IdentityUrl = 'http://localhost:8080/',
  [string]$EnrollmentSecret = 'devenrollmentsecret',
  [string]$DbServer = 'localhost',
  [int]$DbPort = 3306,
  [string]$DbUser = 'root',
  [string]$DbPassword = 'eryph'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$dbConn = "Server=$DbServer;Port=$DbPort;Database=eryph;User Id=$DbUser;Password=$DbPassword"
$rootAnchor = Join-Path $OutDir 'root-only.pem'

# Common mTLS settings.
$env:componentMtls__enabled = 'true'
$env:componentMtls__identityUrl = $IdentityUrl
$env:componentMtls__enrollmentSecret = $EnrollmentSecret
$env:componentMtls__trustAnchorPath = $rootAnchor
$env:RABBITMQ_CONNECTIONSTRING = $Broker

switch ($Component) {
  'Identity' {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'http://localhost:8080'
    $env:ERYPH_IDENTITY_BASEURL = 'http://localhost:8080/'
    $env:componentMtls__certificateDirectory = Join-Path $OutDir 'identity'
    $env:identity__componentEnrollment__secret = $EnrollmentSecret  # Identity self-issues; this is the policy secret
    dotnet run --project (Join-Path $repo 'src\apps\src\Eryph.Identity') -c Release --no-launch-profile
  }
  'Controller' {
    $env:ERYPH_STATEDB_CONNECTIONSTRING = $dbConn
    $env:componentMtls__certificateDirectory = Join-Path $OutDir 'controller'
    # multi-targeted; the Windows TFM manages OVN like eryph-zero
    dotnet run --project (Join-Path $repo 'src\apps\src\Eryph.Controller') --framework net10.0-windows -c Release
  }
  'ComputeApi' {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'http://localhost:8081'
    $env:ERYPH_STATEDB_CONNECTIONSTRING = $dbConn
    $env:ERYPH_IDENTITY_URL = 'http://localhost:8080/identity'
    $env:componentMtls__certificateDirectory = Join-Path $OutDir 'apiendpoint'
    dotnet run --project (Join-Path $repo 'src\apps\src\Eryph.ApiEndpoint') -c Release --no-launch-profile
  }
  'Agent' {
    $env:componentMtls__certificateDirectory = Join-Path $OutDir 'agent'
    dotnet run --project (Join-Path $repo 'src\apps\src\Eryph.Agent') -c Release
  }
}
