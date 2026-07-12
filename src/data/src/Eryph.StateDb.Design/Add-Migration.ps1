#Requires -Version 7.4
<#
    .SYNOPSIS
        Create a new database migration for all supported DBMS.
    .DESCRIPTION
        This script creates a new migration for all supported
        database management systems by invoking the Entity
        Framework tools.

        eryph ships a SINGLE squashed migration ("InitialCreate") per release.
        Schema changes must REGENERATE that one migration, not append a new
        one: the eryph-zero database is reset on schema change (see
        DatabaseResetService), so there is no in-development upgrade path to
        preserve. This script therefore wipes the Migrations folders and
        regenerates them from scratch. Use it instead of a bare
        `dotnet ef migrations add`, which would accumulate per-change
        migrations that only get squashed away again at release time.

        Run this from the Eryph.StateDb.Design directory (the EF tools use
        the current directory as the startup project).
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
