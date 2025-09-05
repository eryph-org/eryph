using System;
using Dbosoft.OVN.Windows;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Storage;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

[UsedImplicitly]
internal class UpdateCatletVMCommandHandler(
    IPowershellEngine engine,
    IHyperVOvsPortManager portManager,
    ITaskMessaging messaging,
    ILogger log,
    IHostInfoProvider hostInfoProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
    : CatletConfigCommandHandler<UpdateCatletVMCommand, ConvergeCatletResult>(engine, messaging, log)
{
    protected override EitherAsync<Error, ConvergeCatletResult> HandleCommand(
        UpdateCatletVMCommand command) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        from hostInfo in hostInfoProvider.GetHostInfoAsync(true).WriteTrace()
        let vmId = command.VmId
        from vmInfo in VmQueries.GetVmInfo(Engine, vmId)
        from currentStorageSettings in VMStorageSettings.FromVm(vmHostAgentConfig, vmInfo).WriteTrace()
        from plannedStorageSettings in VMStorageSettings.Plan(vmHostAgentConfig, command.Config, currentStorageSettings)
            .WriteTrace()
        from _ in EnsureMetadata(vmInfo, command.MetadataId).WriteTrace()
        from substitutedConfig in CatletConfigVariableSubstitutions.SubstituteVariables(command.Config)
            .ToEither()
            .MapLeft(issues => Error.New("The substitution of variables failed.", Error.Many(issues.Map(i => i.ToError()))))
            .ToAsync()
        from vmInfoConsistent in EnsureNameConsistent(vmInfo, substitutedConfig, Engine).WriteTrace()
        from vmInfoConverged in VirtualMachine.Converge(
                vmHostAgentConfig, hostInfo, Engine, portManager, ProgressMessage, vmInfoConsistent,
                substitutedConfig, command.CatletId, command.MachineNetworkSettings,
                plannedStorageSettings, command.ResolvedGenes.ToSeq())
            .WriteTrace()
        let timestamp = DateTimeOffset.UtcNow
        from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, vmInfoConverged, hostInfoProvider).WriteTrace()
        select new ConvergeCatletResult
        {
            VmId = vmInfoConverged.Value.Id,
            Inventory = inventory,
            Timestamp = timestamp,
        };
}
