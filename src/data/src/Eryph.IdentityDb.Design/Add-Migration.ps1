#Requires -Version 7.4
<#
    .SYNOPSIS
        Create the identity-database migration for all supported DBMS.
    .DESCRIPTION
        This script regenerates the identity-database migration for all
        supported database management systems by invoking the Entity
        Framework tools, mirroring the state database's Add-Migration.ps1.

        eryph ships a SINGLE squashed migration ("InitialCreate") per release.
        Schema changes must REGENERATE that one migration, not append a new
        one: the cluster identity database is set up from the generated SQL
        script (see Generate-MigrationSql.ps1) and dev databases are recreated
        on schema change, so there is no in-development upgrade path to
        preserve. This script therefore wipes the Migrations folders and
        regenerates them from scratch. Use it instead of a bare
        `dotnet ef migrations add`, which would accumulate per-change
        migrations that only get squashed away again at release time.

        Run this from the Eryph.IdentityDb.Design directory (the EF tools use
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

Remove-Item -Path "$PSScriptRoot/../Eryph.IdentityDb.Sqlite/Migrations" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$PSScriptRoot/../Eryph.IdentityDb.MySql/Migrations" -Recurse -Force -ErrorAction SilentlyContinue

dotnet build

dotnet ef migrations add `
    --no-build `
    --project "$PSScriptRoot/../Eryph.IdentityDb.Sqlite/Eryph.IdentityDb.Sqlite.csproj" `
    --context SqliteIdentityDbContext `
    $Name

dotnet ef migrations add `
    --no-build `
    --project "$PSScriptRoot/../Eryph.IdentityDb.MySql/Eryph.IdentityDb.MySql.csproj" `
    --context MySqlIdentityDbContext `
    $Name
