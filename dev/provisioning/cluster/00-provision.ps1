<#
.SYNOPSIS
  Provisions the PKI + broker TLS material for a local split-runtime mTLS cluster, using the in-repo
  component CA via the Eryph.ClusterProvision harness. Run elevated. Reference implementation of the
  out-of-repo provisioning glue — see docs/internal/cluster-mtls-bring-up.md.
.PARAMETER OutDir
  Where to write the cluster material (broker certs, trust anchor). Default C:\eryph-dev-cluster.
.PARAMETER BrokerDns
  The DNS name the broker presents / clients connect to. Default 'localhost'.
.PARAMETER BrokerContainer
  podman/docker container name of the RabbitMQ broker to re-key. Default 'eryph-rabbitmq'.
#>
[CmdletBinding()]
param(
  [string]$OutDir = 'C:\eryph-dev-cluster',
  [string]$BrokerDns = 'localhost',
  [string]$BrokerContainer = 'eryph-rabbitmq'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$prov = Join-Path $repo 'dev\provisioning\Eryph.ClusterProvision'
$broker = Join-Path $OutDir 'broker'

Write-Host "== 1. Create/reuse the component CA (root + intermediates) ==" -ForegroundColor Cyan
dotnet run -c Release --project $prov -- init

Write-Host "== 2. Provision broker trust bundle + server certificate from the CA ==" -ForegroundColor Cyan
dotnet run -c Release --project $prov -- provision-broker $BrokerDns $broker

Write-Host "== 3. Export the root-only trust anchor ==" -ForegroundColor Cyan
$rootOnly = Join-Path $OutDir 'root-only.pem'
$bundle = Get-Content (Join-Path $broker 'ca-bundle.pem') -Raw
$first = ($bundle -split '-----END CERTIFICATE-----')[0] + "-----END CERTIFICATE-----`n"
Set-Content -Path $rootOnly -Value $first -NoNewline
Write-Host "  wrote $rootOnly"

Write-Host "== 4. Install the deployment root into LocalMachine\Root ==" -ForegroundColor Cyan
Import-Certificate -FilePath $rootOnly -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
Write-Host "  installed root into LocalMachine\Root"

Write-Host "== 5. Re-key the broker and restart ==" -ForegroundColor Cyan
$env:MSYS_NO_PATHCONV = '1'
Get-Content (Join-Path $broker 'ca-bundle.pem') | & podman exec -i $BrokerContainer tee /etc/rabbitmq/certs/ca.crt    | Out-Null
Get-Content (Join-Path $broker 'server.crt')    | & podman exec -i $BrokerContainer tee /etc/rabbitmq/certs/server.crt | Out-Null
Get-Content (Join-Path $broker 'server.key')    | & podman exec -i $BrokerContainer tee /etc/rabbitmq/certs/server.key | Out-Null
& podman restart $BrokerContainer | Out-Null
Write-Host "  broker re-keyed and restarted"

Write-Host "Provisioning complete. Start components with run-component.ps1." -ForegroundColor Green
