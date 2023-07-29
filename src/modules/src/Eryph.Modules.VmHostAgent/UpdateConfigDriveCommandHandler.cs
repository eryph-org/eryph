using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
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
    VirtualCatletConfigCommandHandler<UpdateVirtualCatletConfigDriveCommand, Unit>
{
    private readonly IHostInfoProvider _hostInfoProvider;

    public UpdateConfigDriveCommandHandler(IPowershellEngine engine, ITaskMessaging messaging, ILogger log, IHostInfoProvider hostInfoProvider) : base(engine, messaging, log)
    {
        _hostInfoProvider = hostInfoProvider;
    }

    protected override EitherAsync<Error, Unit> HandleCommand(UpdateVirtualCatletConfigDriveCommand command)
    {
        var vmId = command.VMId;

        var config = new CatletConfig
        {
            Raising = command.MachineMetadata.RaisingConfig
        };

        var hostSettings = HostSettingsBuilder.GetHostSettings();
        var convergeConfigDrive = Prelude.fun(
            (TypedPsObject<VirtualMachineInfo> vmInfo, VMStorageSettings storageSettings, VMHostMachineData hostInfo) =>
                VirtualMachine.ConvergeConfigDrive(hostSettings, hostInfo, Engine, ProgressMessage, vmInfo, config,
                    command.MachineMetadata, command.MachineNetworkSettings, storageSettings));


        return
            from hostInfo in _hostInfoProvider.GetHostInfoAsync().WriteTrace()
            from vmList in GetVmInfo(vmId, Engine)
            from vmInfo in EnsureSingleEntry(vmList, vmId)
            from metadata in EnsureMetadata(command.MachineMetadata, vmInfo).WriteTrace().ToAsync()
            from currentStorageSettings in VMStorageSettings.FromVM(hostSettings, vmInfo).WriteTrace()
                .Bind(o => o.ToEither(Error.New("Could not find storage settings for VM.")).ToAsync())
            from vmInfoConverged in convergeConfigDrive(vmInfo, currentStorageSettings, hostInfo).WriteTrace().ToAsync()

            select Unit.Default;

    }

}