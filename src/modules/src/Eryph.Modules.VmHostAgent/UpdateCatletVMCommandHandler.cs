using System;
using Dbosoft.OVN.Windows;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.VmManagement;
using Eryph.VmManagement.Storage;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class UpdateCatletVMCommandHandler(
    IPowershellEngine engine,
    IHyperVOvsPortManager portManager,
    IFileSystemService fileSystem,
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
        let vmId = command.VMId
        from optionalVmInfo in GetVmInfo(vmId, Engine)
        from vmInfo in optionalVmInfo.ToEitherAsync(
            Error.New($"The VM with ID {vmId} was not found."))
        from currentStorageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, vmInfo).WriteTrace()
        from plannedStorageSettings in VMStorageSettings.Plan(
                vmHostAgentConfig, LongToString(command.NewStorageId),
                command.Config, currentStorageSettings)
            .WriteTrace()
        from _ in EnsureMetadata(vmInfo, command.MachineMetadata.Id).WriteTrace()
        let genepoolReader = new LocalGenepoolReader(fileSystem, vmHostAgentConfig)
        from fedConfig in CatletFeeding.Feed(
            CatletFeeding.FeedSystemVariables(command.Config, command.MachineMetadata),
            command.ResolvedGenes.ToSeq(),
            genepoolReader)
        from substitutedConfig in CatletConfigVariableSubstitutions.SubstituteVariables(fedConfig)
            .ToEither()
            .MapLeft(issues => Error.New("The substitution of variables failed.", Error.Many(issues.Map(i => i.ToError()))))
            .ToAsync()
        from vmInfoConsistent in EnsureNameConsistent(vmInfo, substitutedConfig, Engine).WriteTrace()
        from vmInfoConverged in VirtualMachine.Converge(
                vmHostAgentConfig, hostInfo, Engine, portManager, ProgressMessage, vmInfoConsistent,
                substitutedConfig, command.MachineMetadata, command.MachineNetworkSettings,
                plannedStorageSettings, command.ResolvedGenes.ToSeq())
            .WriteTrace().ToAsync()
        let timestamp = DateTimeOffset.UtcNow
        from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, vmInfoConverged, hostInfoProvider).WriteTrace()
        select new ConvergeCatletResult
        {
            VmId = vmInfoConverged.Value.Id,
            MetadataId = command.MachineMetadata.Id,
            Inventory = inventory,
            Timestamp = timestamp,
        };
}
