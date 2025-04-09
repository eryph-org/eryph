using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using JetBrains.Annotations;
using Rebus.Handlers;

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
            Config = NormalizeCatletConfig(message.Command.Config),
        };
        return taskMessaging.CompleteTask(message, response);
    }

    // TODO add early validation of networks

    private static CatletConfig NormalizeCatletConfig(CatletConfig config)
    {
        var machineConfig = config;

        if (string.IsNullOrWhiteSpace(machineConfig.Name))
            machineConfig.Name = EryphConstants.DefaultCatletName;

        if (machineConfig.Environment != null)
            machineConfig.Environment = machineConfig.Environment.ToLowerInvariant();

        machineConfig.Drives ??= [];
        machineConfig.NetworkAdapters ??= [];
        machineConfig.Fodder ??= [];
        machineConfig.Variables ??= [];
        machineConfig.Networks ??= [];

        foreach(var fodderConfig in machineConfig.Fodder)
        {
            fodderConfig.Variables ??= [];
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

        return CatletConfigDefaults.ApplyDefaultNetwork(machineConfig);
    }
}