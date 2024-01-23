using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class UpdateConfigDriveCommandHandler : 
    CatletConfigCommandHandler<UpdateCatletConfigDriveCommand, Unit>
{
    private readonly IHostInfoProvider _hostInfoProvider;
    private readonly IHostSettingsProvider _hostSettingsProvider;
    private readonly IVmHostAgentConfigurationManager _vmHostAgentConfigurationManager;

    public UpdateConfigDriveCommandHandler(
        IPowershellEngine engine,
        ITaskMessaging messaging,
        ILogger log,
        IHostInfoProvider hostInfoProvider,
        IHostSettingsProvider hostSettingsProvider,
        IVmHostAgentConfigurationManager vmHostAgentConfigurationManager) : base(engine, messaging, log)
    {
        _hostInfoProvider = hostInfoProvider;
        _hostSettingsProvider = hostSettingsProvider;
        _vmHostAgentConfigurationManager = vmHostAgentConfigurationManager;
    }

    protected override EitherAsync<Error, Unit> HandleCommand(UpdateCatletConfigDriveCommand command)
    {
        var vmId = command.VMId;

        var fodderConfig = new CatletConfig
        {
            Name = command.CatletName,
            Fodder = command.MachineMetadata.Fodder
        };

        var convergeConfigDrive = Prelude.fun(
            (VmHostAgentConfiguration vmHostAgentConfig, TypedPsObject<VirtualMachineInfo> vmInfo, VMStorageSettings storageSettings, VMHostMachineData hostInfo, CatletConfig config) =>
                VirtualMachine.ConvergeConfigDrive(vmHostAgentConfig, hostInfo, Engine, ProgressMessage, vmInfo, config,
                    command.MachineMetadata, command.MachineNetworkSettings, storageSettings));


        return
            from hostSettings in _hostSettingsProvider.GetHostSettings()
            from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
            from hostInfo in _hostInfoProvider.GetHostInfoAsync().WriteTrace()
            from vmList in GetVmInfo(vmId, Engine)
            from vmInfo in EnsureSingleEntry(vmList, vmId)
            from metadata in EnsureMetadata(command.MachineMetadata, vmInfo).WriteTrace().ToAsync()
            from currentStorageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, vmInfo).WriteTrace()
                .Bind(o => o.ToEither(Error.New("Could not find storage settings for VM.")).ToAsync())
            
            from mergedConfig in fodderConfig.BreedAndFeed(vmHostAgentConfig, metadata.ParentConfig).ToAsync()
            from vmInfoConverged in convergeConfigDrive(vmHostAgentConfig, vmInfo, currentStorageSettings, hostInfo, mergedConfig).WriteTrace().ToAsync()

            select Unit.Default;

    }

}