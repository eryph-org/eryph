using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests;

public class StateStoreDbUnitOfWorkTests(ITestOutputHelper outputHelper)
    : InMemoryStateDbTestBase(outputHelper)
{
    /// <summary>
    /// Guards the provider-aware isolation choice in <see cref="StateStoreDbUnitOfWork.Initialize"/>:
    /// SQLite (eryph-zero and this test store) only supports Serializable/ReadUncommitted and throws for
    /// ReadCommitted, so the unit of work must keep the default there. On MariaDB it requests
    /// ReadCommitted instead, so a handler sees rows another process committed after its transaction
    /// began (the fix for the split-runtime "Operation not found").
    /// </summary>
    [Fact]
    public async Task Initialize_uses_a_supported_isolation_level_on_sqlite()
    {
        await using var scope = CreateScope();
        var dbContext = scope.GetInstance<StateStoreContext>();

        var unitOfWork = new StateStoreDbUnitOfWork(dbContext);

        var act = async () =>
        {
            await unitOfWork.Initialize();
            await unitOfWork.Commit();
            await unitOfWork.DisposeAsync();
        };

        await act.Should().NotThrowAsync();
    }
}
