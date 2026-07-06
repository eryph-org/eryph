using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using Eryph.StateDb.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Workflows;

/// <summary>
/// Verifies that <see cref="OperationManager.GetOrCreateAsync"/> persists (commits) the operation
/// before it returns. The dispatcher sends the <c>CreateOperationCommand</c> immediately afterwards;
/// on a multi-process runtime the controller handling that command must be able to read the row, so it
/// has to be durable before the send — not merely tracked. Guards against re-introducing an ambient
/// transaction that would defer the commit until after the command is on the wire.
/// </summary>
public class OperationManagerTests(ITestOutputHelper outputHelper)
    : InMemoryStateDbTestBase(outputHelper)
{
    [Fact]
    public async Task GetOrCreateAsync_commits_the_operation_before_returning()
    {
        var operationId = Guid.NewGuid();
        var data = new OperationDataRecord(EryphConstants.DefaultTenantId, "trace-id", "test");

        await using (var scope = CreateScope())
        {
            var db = scope.GetInstance<StateStoreContext>();
            var manager = new OperationManager(db, new WorkflowOptions(), NullLogger<OperationManager>.Instance);

            await manager.GetOrCreateAsync(operationId, new object(), DateTimeOffset.UtcNow, data, null);

            // Intentionally do NOT commit this scope: the operation must already be durable. If it is
            // only tracked (the old behaviour, where an ambient transaction deferred the commit), the
            // fresh context below would not find it.
        }

        await using (var verifyScope = CreateScope())
        {
            var db = verifyScope.GetInstance<StateStoreContext>();

            var persisted = await db.Operations.FindAsync(operationId);
            persisted.Should().NotBeNull(
                "GetOrCreateAsync must commit the operation before the dispatcher sends its command");
        }
    }
}
