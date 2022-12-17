using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class UpdateCatletNetworksCommandResponse
{
    public MachineNetworkSettings[] NetworkSettings { get; set; }

}