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
    [Parameter()]
    [string]
    $Name
)

$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = 'Stop'

dotnet build

dotnet ef migrations add `
    --json `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.Sqlite/Eryph.StateDb.Sqlite.csproj" `
    --context SqliteStateStoreContext `
    $Name

dotnet ef migrations add `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.SqlServer/Eryph.StateDb.SqlServer.csproj" `
    --context SqlServerStateStoreContext `
    $Name

dotnet ef migrations add `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.MySql/Eryph.StateDb.MySql.csproj" `
    --context MySqlStateStoreContext `
    $Name

# The migration code must be built again before generating the SQL scripts.
dotnet build

dotnet ef migrations script `
    --no-build `
    --idempotent `
    --project "$PSScriptRoot/../Eryph.StateDb.SqlServer/Eryph.StateDb.SqlServer.csproj" `
    --context SqlServerStateStoreContext `
    --output "$PSScriptRoot/../../../../migrations/sqlserver.sql"

dotnet ef migrations script `
    --no-build `
    --idempotent `
    --project "$PSScriptRoot/../Eryph.StateDb.MySql/Eryph.StateDb.MySql.csproj" `
    --context MySqlStateStoreContext `
    --output "$PSScriptRoot/../../../../migrations/mysql.sql"
