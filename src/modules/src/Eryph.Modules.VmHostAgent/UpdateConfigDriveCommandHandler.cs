using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
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
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class UpdateConfigDriveCommandHandler : VirtualCatletConfigCommandHandler,
    IHandleMessages<OperationTask<UpdateVirtualCatletConfigDriveCommand>>
{
    private readonly IHostInfoProvider _hostInfoProvider;

    public UpdateConfigDriveCommandHandler(IPowershellEngine engine, IBus bus, ILogger log, IHostInfoProvider hostInfoProvider) : base(engine, bus, log)
    {
        _hostInfoProvider = hostInfoProvider;
    }

    public Task Handle(OperationTask<UpdateVirtualCatletConfigDriveCommand> message)
    {
        var command = message.Command;
        var vmId = command.VMId;

        OperationId = message.OperationId;
        TaskId = message.TaskId;

        
        var config = new CatletConfig
        {
            Raising = message.Command.MachineMetadata.RaisingConfig
        };

        var hostSettings = HostSettingsBuilder.GetHostSettings();
        var convergeConfigDrive = Prelude.fun(
            (TypedPsObject<VirtualMachineInfo> vmInfo, VMStorageSettings storageSettings, VMHostMachineData hostInfo) =>
                VirtualMachine.ConvergeConfigDrive(hostSettings, hostInfo, Engine, ProgressMessage, vmInfo, config, 
                    message.Command.MachineMetadata, command.MachineNetworkSettings, storageSettings));


        var chain =
            from hostInfo in _hostInfoProvider.GetHostInfoAsync().WriteTrace()
            from vmList in GetVmInfo(vmId, Engine)
            from vmInfo in EnsureSingleEntry(vmList, vmId)
            from metadata in EnsureMetadata(message.Command.MachineMetadata, vmInfo).WriteTrace().ToAsync()
            from currentStorageSettings in VMStorageSettings.FromVM(hostSettings, vmInfo).WriteTrace()
                .Bind(o => o.ToEither(Error.New("Could not find storage settings for VM.")).ToAsync())
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