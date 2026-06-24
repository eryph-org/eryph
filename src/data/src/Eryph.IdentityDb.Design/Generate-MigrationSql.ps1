#Requires -Version 7.4
<#
    .SYNOPSIS
        Generates the identity-database migration SQL script.
    .DESCRIPTION
        Emits the idempotent MariaDB setup script for the identity database by invoking the Entity
        Framework tools, mirroring the state database's Generate-MigrationSql.ps1. The cluster identity
        database is set up from this SQL script (and the `eryph-identity create-db` command in dev),
        not by migrating in-process.
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
    --project "$PSScriptRoot/../Eryph.IdentityDb.MySql/Eryph.IdentityDb.MySql.csproj" `
    --context MySqlIdentityDbContext `
    --output (Join-Path $Output "identity.mysql.sql")
