using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.VmManagement;
using HyperVPlus.VmManagement.Data;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class InventoryRequestedEventHandler : IHandleMessages<InventoryRequestedEvent>
    {

        private readonly IPowershellEngine _engine;
        private readonly IBus _bus;

        public InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine)
        {
            _bus = bus;
            _engine = engine;
        }

        public Task Handle(InventoryRequestedEvent message) => 
            _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm"))
                .ToAsync()
                .IfRightAsync(vms => _bus.Send(VmsToInventory(vms)));

        private static UpdateInventoryCommand VmsToInventory(ISeq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            var inventory = vms.Map(vm => new VmInventoryInfo
            {
                Id = vm.Value.Id,
                Status = MapVmInfoStatusToVmStatus(vm.Value.State),
                Name = vm.Value.Name,
                IpV4Addresses = GetAddressesByFamily(vm, AddressFamily.InterNetwork),
                IpV6Addresses = GetAddressesByFamily(vm, AddressFamily.InterNetworkV6),
            }).ToList();

            return new UpdateInventoryCommand
            {
                AgentName = Environment.MachineName,
                Inventory = inventory
               
            };
        }

        private static VmStatus MapVmInfoStatusToVmStatus(VirtualMachineState valueState)
        {
            switch (valueState)
            {
                case VirtualMachineState.Other:
                    return VmStatus.Stopped;
                case VirtualMachineState.Running:
                    return VmStatus.Running;
                case VirtualMachineState.Off:
                    return VmStatus.Stopped;
                case VirtualMachineState.Stopping:
                    return VmStatus.Stopped;
                case VirtualMachineState.Saved:
                    return VmStatus.Stopped;
                case VirtualMachineState.Paused:
                    return VmStatus.Stopped;
                case VirtualMachineState.Starting:
                    return VmStatus.Stopped;
                case VirtualMachineState.Reset:
                    return VmStatus.Stopped;
                case VirtualMachineState.Saving:
                    return VmStatus.Stopped;
                case VirtualMachineState.Pausing:
                    return VmStatus.Stopped;
                case VirtualMachineState.Resuming:
                    return VmStatus.Stopped;
                case VirtualMachineState.FastSaved:
                    return VmStatus.Stopped;
                case VirtualMachineState.FastSaving:
                    return VmStatus.Stopped;
                case VirtualMachineState.ForceShutdown:
                    return VmStatus.Stopped;
                case VirtualMachineState.ForceReboot:
                    return VmStatus.Stopped;
                case VirtualMachineState.RunningCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.OffCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.StoppingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.SavedCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.PausedCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.StartingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.ResetCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.SavingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.PausingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.ResumingCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.FastSavedCritical:
                    return VmStatus.Stopped;
                case VirtualMachineState.FastSavingCritical:
                    return VmStatus.Stopped;
                default:
                    throw new ArgumentOutOfRangeException(nameof(valueState), valueState, null);
            }
        }

        private static List<string> GetAddressesByFamily(TypedPsObject<VirtualMachineInfo> vm, AddressFamily family)
        {
            return vm.Value.NetworkAdapters.Bind(adapter => adapter.IPAddresses.Where(a =>
            {               
                var ipAddress = IPAddress.Parse(a);
                return ipAddress.AddressFamily == family;
            })).ToList();
        }



    }
}