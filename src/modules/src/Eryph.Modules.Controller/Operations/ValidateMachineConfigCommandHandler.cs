using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Resources.Machines;
using Eryph.Resources.Machines.Config;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Operations
{
    [UsedImplicitly]
    public class ValidateMachineConfigCommandHandler : IHandleMessages<OperationTask<ValidateMachineConfigCommand>>
    {
        private readonly IBus _bus;

        public ValidateMachineConfigCommandHandler(IBus bus)
        {
            _bus = bus;
        }

        public Task Handle(OperationTask<ValidateMachineConfigCommand> message)
        {
            message.Command.Config = NormalizeMachineConfig(message.Command.MachineId, message.Command.Config);
            return _bus.SendLocal(OperationTaskStatusEvent.Completed(message.OperationId, message.TaskId, message.Command));
        }

        private static MachineConfig NormalizeMachineConfig(
#pragma warning restore 1998
            Guid machineId, MachineConfig config)
        {
            var machineConfig = config;

            if (machineConfig.VM == null)
                machineConfig.VM = new VirtualMachineConfig();

            if (string.IsNullOrWhiteSpace(machineConfig.Name) && machineId == Guid.Empty)
                //TODO generate a random name here
                machineConfig.Name = "eryph-machine";


            if (machineConfig.VM.Cpu == null)
                machineConfig.VM.Cpu = new VirtualMachineCpuConfig();

            if (machineConfig.VM.Memory == null)
                machineConfig.VM.Memory = new VirtualMachineMemoryConfig();

            if (machineConfig.VM.Drives == null)
                machineConfig.VM.Drives = new List<VirtualMachineDriveConfig>();

            if (machineConfig.VM.NetworkAdapters == null)
                machineConfig.VM.NetworkAdapters = new List<VirtualMachineNetworkAdapterConfig>();

            if (machineConfig.Provisioning == null)
                machineConfig.Provisioning = new VirtualMachineProvisioningConfig();

            if (string.IsNullOrWhiteSpace(machineConfig.Provisioning.Hostname))
                machineConfig.Provisioning.Hostname = machineConfig.Name;

            foreach (var adapterConfig in machineConfig.VM.NetworkAdapters)
            {
                if (adapterConfig.MacAddress != null)
                {
                    adapterConfig.MacAddress = adapterConfig.MacAddress.Replace("-", "");
                    adapterConfig.MacAddress = adapterConfig.MacAddress.Replace(":", "");
                    adapterConfig.MacAddress = adapterConfig.MacAddress.ToLowerInvariant();
                }
                else
                {
                    adapterConfig.MacAddress = "";
                }
            }

            foreach (var driveConfig in machineConfig.VM.Drives)
            {
                if (!driveConfig.Type.HasValue)
                    driveConfig.Type = VirtualMachineDriveType.VHD;

                if (driveConfig.Size == 0)
                    driveConfig.Size = null;
            }

            return machineConfig;
        }
    }
}