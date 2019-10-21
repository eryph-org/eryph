using System;

namespace Haipa.Messages.Commands
{
    public interface IMachineCommand 
    {
        Guid MachineId { get; set; }
    }
}