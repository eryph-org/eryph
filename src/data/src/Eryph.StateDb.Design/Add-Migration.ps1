
dotnet build

dotnet ef migrations has-pending-model-changes `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.Sqlite/Eryph.StateDb.Sqlite.csproj" `
    --context SqliteStateStoreContext `

dotnet ef migrations remove `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.Sqlite/Eryph.StateDb.Sqlite.csproj" `
    --context SqliteStateStoreContext `

dotnet ef migrations add `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.Sqlite/Eryph.StateDb.Sqlite.csproj" `
    --context SqliteStateStoreContext `
    MyMigration

dotnet ef migrations script `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.Sqlite/Eryph.StateDb.Sqlite.csproj" `
    --context SqliteStateStoreContext

dotnet ef migrations add `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.SqlServer/Eryph.StateDb.SqlServer.csproj" `
    --context SqlServerStateStoreContext `
    MyMigration

dotnet ef migrations script `
    --no-build `
    --project "$PSScriptRoot/../Eryph.StateDb.SqlServer/Eryph.StateDb.SqlServer.csproj" `
    --context SqlServerStateStoreContext