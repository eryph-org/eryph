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

        private static CatletConfig NormalizeCatletConfig(Guid machineId, CatletConfig config)
        {
            var machineConfig = config;

            if (string.IsNullOrWhiteSpace(machineConfig.Name) && machineId == Guid.Empty)
                machineConfig.Name = "catlet";

            if (machineConfig.Environment != null)
                machineConfig.Environment = machineConfig.Environment.ToLowerInvariant();

            machineConfig.Cpu ??= new CatletCpuConfig();
            machineConfig.Memory ??= new CatletMemoryConfig();
            machineConfig.Drives ??= [];
            machineConfig.NetworkAdapters ??= [];
            machineConfig.Fodder ??= [];
            machineConfig.Variables ??= [];
            machineConfig.Networks ??=
            [
                new CatletNetworkConfig
                {
                    Name = "default"
                }
            ];

            foreach(var fodderConfig in machineConfig.Fodder)
            {
                fodderConfig.Variables ??= [];
            }

            for (var i = 0; i < machineConfig.Networks.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(machineConfig.Networks[i].AdapterName))
                    machineConfig.Networks[i].AdapterName = $"eth{i}";
            }

            if (string.IsNullOrWhiteSpace(machineConfig.Hostname))
                machineConfig.Hostname =  machineConfig.Name;

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
                driveConfig.Type ??= CatletDriveType.VHD;

                if (driveConfig.Size == 0)
                    driveConfig.Size = null;
            }

            return machineConfig;
        }
    }
}
