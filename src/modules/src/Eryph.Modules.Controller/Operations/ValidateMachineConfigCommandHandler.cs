using System;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Messages.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Operations
{
    [UsedImplicitly]
    public class ValidateCatletConfigCommandHandler : IHandleMessages<OperationTask<ValidateCatletConfigCommand>>
    {
        private readonly IBus _bus;

        public ValidateCatletConfigCommandHandler(IBus bus)
        {
            _bus = bus;
        }

        public Task Handle(OperationTask<ValidateCatletConfigCommand> message)
        {
            message.Command.Config = NormalizeCatletConfig(message.Command.MachineId, message.Command.Config);
            return _bus.CompleteTask(message, message.Command);
        }

        private static CatletConfig NormalizeCatletConfig(
#pragma warning restore 1998
            Guid machineId, CatletConfig config)
        {
            var machineConfig = config;

            if (machineConfig.VCatlet == null)
                machineConfig.VCatlet = new VirtualCatletConfig();

            if (string.IsNullOrWhiteSpace(machineConfig.Name) && machineId == Guid.Empty)
                //TODO generate a random name here
                machineConfig.Name = "eryph-machine";


            if (machineConfig.VCatlet.Cpu == null)
                machineConfig.VCatlet.Cpu = new VirtualCatletCpuConfig();

            if (machineConfig.VCatlet.Memory == null)
                machineConfig.VCatlet.Memory = new VirtualCatletMemoryConfig();

            if (machineConfig.VCatlet.Drives == null)
                machineConfig.VCatlet.Drives = Array.Empty<VirtualCatletDriveConfig>();

            if (machineConfig.VCatlet.NetworkAdapters == null)
                machineConfig.VCatlet.NetworkAdapters = Array.Empty<VirtualCatletNetworkAdapterConfig>();

            if (machineConfig.Raising == null)
                machineConfig.Raising = new CatletRaisingConfig();

            if (machineConfig.Networks == null)
            {
                machineConfig.Networks =
                    new []{ new CatletNetworkConfig
                    {
                        Name = "default"
                    } };
            }

            for (var i = 0; i < machineConfig.Networks.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(machineConfig.Networks[i].AdapterName))
                    machineConfig.Networks[i].AdapterName = $"eth{i}";
            }

            if (string.IsNullOrWhiteSpace(machineConfig.Raising.Hostname))
                machineConfig.Raising.Hostname = machineConfig.Name;

            foreach (var adapterConfig in machineConfig.VCatlet.NetworkAdapters)
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

            foreach (var driveConfig in machineConfig.VCatlet.Drives)
            {
                if (!driveConfig.Type.HasValue)
                    driveConfig.Type = VirtualCatletDriveType.VHD;

                if (driveConfig.Size == 0)
                    driveConfig.Size = null;
            }

            return machineConfig;
        }

    }
}