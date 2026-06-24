using Xunit;

namespace Eryph.StateDb.TestBase;

public interface IDatabaseFixture : IAsyncLifetime
{
    string GetConnectionString(string databaseName);
}
