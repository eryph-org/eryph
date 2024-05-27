#Requires -Version 7.4
<#
    .SYNOPSIS
        Generates the migration SQL scripts for all supported DBMS.
    .DESCRIPTION
        This script generates the migration SQL scrips for all supported
        database management systems by invoking the Entity
        Framework tools.
#>
[CmdletBinding()]
param(
    # Indicates that the build should be skipped.
    [Parameter()]
    [switch]
    $NoBuild,
    # The output directory for the SQL scripts.
    [Parameter()]
    [string]
    $Output = "$PSScriptRoot/../../../../migrations"
)

$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = 'Stop'

if (-not $NoBuild) {
	dotnet build
}

dotnet ef migrations script `
    --no-build `
    --idempotent `
    --project "$PSScriptRoot/../Eryph.StateDb.MySql/Eryph.StateDb.MySql.csproj" `
    --context MySqlStateStoreContext `
    --output (Join-Path $Output "mysql.sql")
