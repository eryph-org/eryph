using System;

namespace Haipa.Messages.Resources.Machines
{
    public interface IVMCommand
    {
        long MachineId { get; set; }
        Guid VMId { get; set; }
    }
}