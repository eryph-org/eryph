using System;

namespace Haipa.Messages.Resources.Machines
{
    public interface IVMCommand
    {
        Guid MachineId { get; set; }
        Guid VMId { get; set; }
    }
}