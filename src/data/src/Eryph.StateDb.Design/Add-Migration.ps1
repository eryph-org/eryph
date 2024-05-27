#Requires -Version 7.4
<#
    .SYNOPSIS
        Create a new database migration for all supported DBMS.
    .DESCRIPTION
        This script creates a new migration for all supported
        database management systems by invoking the Entity
        Framework tools.
#>
[CmdletBinding()]
param(
    # The name of the new migration
    [Parameter(Mandatory=$true)]
    [string]
    $Name
)

$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = 'Stop'

Remove-Item -Path "$PSScriptRoot/../Eryph.StateDb.Sqlite/Migrations" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$PSScriptRoot/../Eryph.StateDb.MySql/Migrations" -Recurse -Force -ErrorAction SilentlyContinue

dotnet build

dotnet ef migrations add `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.Sqlite/Eryph.StateDb.Sqlite.csproj" `
    --context SqliteStateStoreContext `
    $Name

dotnet ef migrations add `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.MySql/Eryph.StateDb.MySql.csproj" `
    --context MySqlStateStoreContext `
    $Name
