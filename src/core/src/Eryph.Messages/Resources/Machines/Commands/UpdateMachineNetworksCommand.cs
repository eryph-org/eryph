using System;
using Eryph.ConfigModel.Machine;
using Eryph.Resources.Machines;
using LanguageExt;

namespace Eryph.Messages.Resources.Machines.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateMachineNetworksCommand
{
    public Guid ProjectId { get; set; }
    public MachineConfig Config { get; set; }
    public Guid MachineId { get; set; }
}

public class UpdateMachineNetworksCommandResponse
{
    public MachineNetworkSettings[] NetworkSettings { get; set; }

}