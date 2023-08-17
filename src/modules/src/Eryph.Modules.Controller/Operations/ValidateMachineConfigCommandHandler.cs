using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Messages.Resources.Catlets.Commands;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Operations
{
    [UsedImplicitly]
    public class ValidateCatletConfigCommandHandler : IHandleMessages<OperationTask<ValidateCatletConfigCommand>>
    {
        private readonly ITaskMessaging _taskMessaging;

        public ValidateCatletConfigCommandHandler(ITaskMessaging taskMessaging)
        {
            _taskMessaging = taskMessaging;
        }

        public Task Handle(OperationTask<ValidateCatletConfigCommand> message)
        {
            message.Command.Config = NormalizeCatletConfig(message.Command.MachineId, message.Command.Config);
            return _taskMessaging.CompleteTask(message, message.Command);
        }

        private static CatletConfig NormalizeCatletConfig(
#pragma warning restore 1998
            Guid machineId, CatletConfig config)
        {
            var machineConfig = config;


            if (string.IsNullOrWhiteSpace(machineConfig.Name) && machineId == Guid.Empty)
                machineConfig.Name = $"catlet";


            if (machineConfig.Cpu == null)
                machineConfig.Cpu = new CatletCpuConfig();

            if (machineConfig.Memory == null)
                machineConfig.Memory = new CatletMemoryConfig();

            if (machineConfig.Drives == null)
                machineConfig.Drives = Array.Empty<CatletDriveConfig>();

            if (machineConfig.NetworkAdapters == null)
                machineConfig.NetworkAdapters = Array.Empty<CatletNetworkAdapterConfig>();

            if (machineConfig.Fodder == null)
                machineConfig.Fodder = Array.Empty<FodderConfig>();

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

            if (string.IsNullOrWhiteSpace(machineConfig.Hostname))
                machineConfig.Hostname =  machineConfig.Label;

            foreach (var adapterConfig in machineConfig.NetworkAdapters)
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

            foreach (var driveConfig in machineConfig.Drives)
            {
                if (!driveConfig.Type.HasValue)
                    driveConfig.Type = CatletDriveType.VHD;

                if (driveConfig.Size == 0)
                    driveConfig.Size = null;
            }

            return machineConfig;
        }

    }
}