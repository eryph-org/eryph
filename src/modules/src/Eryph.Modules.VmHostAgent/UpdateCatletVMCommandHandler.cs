using System;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
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
        from vmList in GetVmInfo(vmId, Engine)
        from vmInfo in EnsureSingleEntry(vmList, vmId)
        from currentStorageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, vmInfo).WriteTrace()
        from plannedStorageSettings in VMStorageSettings.Plan(
                vmHostAgentConfig, LongToString(command.NewStorageId),
                command.Config, currentStorageSettings)
            .WriteTrace()
        from metadata in EnsureMetadata(command.MachineMetadata, vmInfo).WriteTrace().ToAsync()
        let genepoolReader = new LocalGenepoolReader(vmHostAgentConfig)
        from fedConfig in CatletFeeding.Feed(
                CatletFeeding.FeedSystemVariables(command.Config, metadata),
                genepoolReader)
            .ToAsync()
        from substitutedConfig in CatletConfigVariableSubstitutions.SubstituteVariables(fedConfig)
            .ToEither()
            .MapLeft(issues => Error.New("The substitution of variables failed.", Error.Many(issues.Map(i => i.ToError()))))
            .ToAsync()
        from vmInfoConsistent in EnsureNameConsistent(vmInfo, substitutedConfig, Engine).WriteTrace()
        from vmInfoConverged in VirtualMachine.Converge(
                vmHostAgentConfig, hostInfo, Engine, ProgressMessage, vmInfoConsistent,
                substitutedConfig, metadata, command.MachineNetworkSettings, plannedStorageSettings)
            .WriteTrace().ToAsync()
        from inventory in CreateMachineInventory(Engine, vmHostAgentConfig, vmInfoConverged, hostInfoProvider).WriteTrace()
        select new ConvergeCatletResult
        {
            Inventory = inventory,
            MachineMetadata = metadata,
            Timestamp = DateTimeOffset.UtcNow,
        };
}
