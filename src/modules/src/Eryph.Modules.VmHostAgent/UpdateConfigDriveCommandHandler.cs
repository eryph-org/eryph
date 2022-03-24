using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Resources.Machines;
using Eryph.Resources.Machines.Config;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class UpdateConfigDriveCommandHandler : VirtualMachineConfigCommandHandler,
    IHandleMessages<OperationTask<UpdateVirtualMachineConfigDriveCommand>>
{
    private readonly IHostInfoProvider _hostInfoProvider;

    public UpdateConfigDriveCommandHandler(IPowershellEngine engine, IBus bus, ILogger log, IHostInfoProvider hostInfoProvider) : base(engine, bus, log)
    {
        _hostInfoProvider = hostInfoProvider;
    }

    public Task Handle(OperationTask<UpdateVirtualMachineConfigDriveCommand> message)
    {
        var command = message.Command;
        var vmId = command.VMId;

        OperationId = message.OperationId;
        TaskId = message.TaskId;

        
        var config = new MachineConfig
        {
            Provisioning = message.Command.MachineMetadata.ProvisioningConfig
        };

        var hostSettings = HostSettingsBuilder.GetHostSettings();
        var convergeConfigDrive = Prelude.fun(
            (TypedPsObject<VirtualMachineInfo> vmInfo, VMStorageSettings storageSettings, VMHostMachineData hostInfo) =>
                VirtualMachine.ConvergeConfigDrive(hostSettings, hostInfo, Engine, ProgressMessage, vmInfo, config, message.Command.MachineMetadata, storageSettings));


        var chain =
            from hostInfo in _hostInfoProvider.GetHostInfoAsync().WriteTrace().ToAsync()
            from vmList in GetVmInfo(vmId, Engine).ToAsync()
            from vmInfo in EnsureSingleEntry(vmList, vmId).ToAsync()
            from metadata in EnsureMetadata(message.Command.MachineMetadata, vmInfo).WriteTrace().ToAsync()
            from currentStorageSettings in VMStorageSettings.FromVM(hostSettings, vmInfo).WriteTrace()
                .BindAsync(o => o.ToEither(new PowershellFailure { Message = "Could not find storage settings for VM." }))
                .ToAsync()        
            from vmInfoConverged in convergeConfigDrive(vmInfo, currentStorageSettings, hostInfo).WriteTrace().ToAsync()

            select Unit.Default;

        return chain.MatchAsync(
            LeftAsync: HandleError,
            RightAsync: async result =>
            {

                return await Bus.Publish(OperationTaskStatusEvent.Completed(OperationId, TaskId, result))
                    .ToUnit().ConfigureAwait(false);
            });
    }
}