using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Messages.Resources.Catlets.Commands;
using LanguageExt;
using JetBrains.Annotations;
using Rebus.Handlers;

using static LanguageExt.Prelude;
using Eryph.Core;

namespace Eryph.Modules.Controller.Operations;

[UsedImplicitly]
public class ValidateCatletConfigCommandHandler(
    ITaskMessaging taskMessaging)
    : IHandleMessages<OperationTask<ValidateCatletConfigCommand>>
{
    public Task Handle(OperationTask<ValidateCatletConfigCommand> message)
    {
        var response = new ValidateCatletConfigCommandResponse
        {
            Config = NormalizeCatletConfig(message.Command.Config, message.Command.IsUpdate),
        };
        return taskMessaging.CompleteTask(message, response);
    }

    private static CatletConfig NormalizeCatletConfig(CatletConfig config, bool isUpdate)
    {
        var machineConfig = config;

        if (string.IsNullOrWhiteSpace(machineConfig.Name) && isUpdate)
            machineConfig.Name = "catlet";

        if (machineConfig.Environment != null)
            machineConfig.Environment = machineConfig.Environment.ToLowerInvariant();

        machineConfig.Cpu ??= isUpdate
            ? null
            : new CatletCpuConfig { Count = EryphConstants.DefaultCatletCpuCount };
        machineConfig.Memory ??= isUpdate
            ? null
            : new CatletMemoryConfig { Startup = EryphConstants.DefaultCatletMemoryMb };
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