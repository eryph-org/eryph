using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.Extensions.Logging;
using Quartz;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Inventory;

internal class VirtualDiskCleanupJob(Container container) : IJob
{
    public static readonly JobKey Key = new(nameof(VirtualDiskCleanupJob));

    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var stateStore = container.GetInstance<IStateStore>();

        var disks = await stateStore.For<VirtualDisk>().ListAsync(
            new VirtualDiskSpecs.FindDeleted(DateTimeOffset.UtcNow.AddHours(-1)));
        await stateStore.For<VirtualDisk>().DeleteRangeAsync(disks);
        
        await stateStore.SaveChangesAsync();
    }
}
