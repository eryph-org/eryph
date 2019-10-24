using System;

namespace Haipa.Messages.Commands
{
    public interface IMachineCommand 
    {
        Guid MachineId { get; set; }
    }

    public interface IHostAgentCommand
    {
        string AgentName { get; set; }
    }
}