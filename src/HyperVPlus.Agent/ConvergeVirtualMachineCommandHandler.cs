using System;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.VmManagement;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport;
using static LanguageExt.Prelude;


// ReSharper disable ArgumentsStyleAnonymousFunction

namespace HyperVPlus.Agent
{
    internal class ConvergeTaskRequestedEventHandler : IHandleMessages<ConvergeVirtualMachineRequestedEvent>
    {
        private readonly IPowershellEngine _engine;
        private readonly IVirtualMachineInfoProvider _infoProvider;
        private readonly IBus _bus;
        private Guid _correlationid;

        public ConvergeTaskRequestedEventHandler(
            IPowershellEngine engine,
            IBus bus,
            IVirtualMachineInfoProvider infoProvider)
        {
            _engine = engine;
            _bus = bus;
            _infoProvider = infoProvider;
        }

        public async Task Handle(ConvergeVirtualMachineRequestedEvent command)
        {
            _correlationid = command.CorellationId;

            await _infoProvider.GetInfoAsync(command.Config.Name).MapAsync(async (vmInfo) =>
            {
                await Converge.Definition(_engine, vmInfo, command.Config, ProgressMessage).ConfigureAwait(false);

                command.Config.Disks.Iter(async (disk) =>
                    await Converge.Disk(_engine, vmInfo, disk, command.Config, ProgressMessage).ConfigureAwait(false));
                command.Config.Networks.Iter(async (network) =>
                    await Converge.Network(_engine, vmInfo, network, command.Config, ProgressMessage)
                        .ConfigureAwait(false));

                await ProgressMessage("Generate Virtual Machine provisioning disk").ConfigureAwait(false);

                await Converge.CloudInit(
                    _engine, command.Config.Path,
                    command.Config.Hostname,
                    command.Config.Provisioning.UserData,
                    vmInfo).ConfigureAwait(false);

            }).ConfigureAwait(false);
            
            await ProgressMessage("Converged").ConfigureAwait(false);


                //await _bus.SendLocal(result.ToEvent(command.CorellationId));

        }

        private async Task ProgressMessage(string message)
        {
            using (var scope = new RebusTransactionScope())
            {
                await _bus.Send(new ConvergeVirtualMachineProgressEvent
                {
                    CorellationId = _correlationid,
                    Message = message
                }).ConfigureAwait(false);

                // commit it like this
                await scope.CompleteAsync().ConfigureAwait(false);
            }


        }
    }
}