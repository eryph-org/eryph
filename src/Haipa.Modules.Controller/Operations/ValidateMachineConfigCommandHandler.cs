using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haipa.Messages.Commands;
using Haipa.Messages.Operations;
using Haipa.VmConfig;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Operations
{
    [UsedImplicitly]
    public class ValidateMachineConfigCommandHandler : IHandleMessages<ValidateMachineConfigCommand>
    {
        private readonly IBus _bus;

        public ValidateMachineConfigCommandHandler(IBus bus)
        {
            _bus = bus;
        }

        public Task Handle(ValidateMachineConfigCommand message)
        {
            message.Config = NormalizeMachineConfig(message.MachineId, message.Config);
            return _bus.SendLocal(OperationTaskStatusEvent.Completed(message.OperationId, message.TaskId, message));

        }

        private static MachineConfig NormalizeMachineConfig(
#pragma warning restore 1998
            Guid machineId, MachineConfig config)
        {
            var machineConfig = config;

            if (machineConfig.VM == null)
                machineConfig.VM = new VirtualMachineConfig();

            if (string.IsNullOrWhiteSpace(machineConfig.Name) && machineId == Guid.Empty)
            {
                //TODO generate a random name here
                machineConfig.Name = "haipa-machine";
            }

            if (machineConfig.Image == null)
                machineConfig.Image = new MachineImageConfig();


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

                if (string.IsNullOrWhiteSpace(adapterConfig.SwitchName))
                    adapterConfig.SwitchName = "Default Switch";
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