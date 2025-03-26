using System.IO.Abstractions;
using Dbosoft.OVN.Windows;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Storage;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class UpdateConfigDriveCommandHandler(
    IPowershellEngine engine,
    IHyperVOvsPortManager portManager,
    ITaskMessaging messaging,
    ILogger log,
    IFileSystemService fileSystem,
    IHostInfoProvider hostInfoProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
    : CatletConfigCommandHandler<UpdateCatletConfigDriveCommand, Unit>(engine, messaging, log)
{
    protected override EitherAsync<Error, Unit> HandleCommand(
        UpdateCatletConfigDriveCommand command) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        from hostInfo in hostInfoProvider.GetHostInfoAsync().WriteTrace()
        let vmId = command.VMId
        from vmInfo in VmQueries.GetVmInfo(Engine, vmId)
        from metadata in EnsureMetadata(vmInfo, command.MachineMetadata.Id).WriteTrace()
        from currentStorageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, vmInfo).WriteTrace()
            .Bind(o => o.ToEither(Error.New("Could not find storage settings for VM.")).ToAsync())
        let genepoolReader = new LocalGenepoolReader(fileSystem, vmHostAgentConfig)
        from fedConfig in CatletFeeding.Feed(
            CatletFeeding.FeedSystemVariables(command.Config, command.MachineMetadata),
            command.ResolvedGenes.ToSeq(),
            genepoolReader)
        from substitutedConfig in CatletConfigVariableSubstitutions.SubstituteVariables(fedConfig)
            .ToEither()
            .MapLeft(issues => Error.New("The substitution of variables failed.", Error.Many(issues.Map(i => i.ToError()))))
            .ToAsync()
        from vmInfoConverged in VirtualMachine.ConvergeConfigDrive(
                vmHostAgentConfig, hostInfo, Engine, portManager, ProgressMessage, vmInfo,
                substitutedConfig, command.MachineMetadata, currentStorageSettings)
            .WriteTrace().ToAsync()
        select Unit.Default;
}
