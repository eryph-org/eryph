using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Disks;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

[UsedImplicitly]
internal class DiskStoresChangedEventHandler(
    IBus bus,
    IFileSystemService fileSystemService,
    ILogger log,
    IPowershellEngine powershellEngine,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    WorkflowOptions workflowOptions)
    : IHandleMessages<DiskStoresChangedEvent>
{
    public async Task Handle(DiskStoresChangedEvent message)
    {
        var result = await InventoryDisks().Run();
        result.IfFail(e => { log.LogError(e, "The disk inventory has failed."); });
        if (result.IsFail)
            return;

        await bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, result.ToOption().ValueUnsafe());
    }

    private Aff<UpdateDiskInventoryCommand> InventoryDisks() =>
        from _ in SuccessAff(unit)
        let timestamp = DateTimeOffset.UtcNow
        from hostSettings in hostSettingsProvider.GetHostSettings()
            .ToAff(identity)
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
            .ToAff(identity)
        from diskInfos in DiskStoreInventory.InventoryStores(
            fileSystemService, powershellEngine, vmHostAgentConfig)
        from __ in diskInfos.Lefts()
            .Map(e =>
            {
                log.LogError(e, "Inventory of virtual disk failed");
                return SuccessEff(unit);
            })
            .Sequence()
        select new UpdateDiskInventoryCommand
        {
            AgentName = Environment.MachineName,
            Timestamp = timestamp,
            Inventory = diskInfos.Rights().ToList()
        };
}
