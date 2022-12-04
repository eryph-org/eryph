using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Machines.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateMachineNetworksCommand
{
    public Guid ProjectId { get; set; }
    public CatletConfig Config { get; set; }
    public Guid MachineId { get; set; }
}

public class UpdateMachineNetworksCommandResponse
{
    public MachineNetworkSettings[] NetworkSettings { get; set; }

}