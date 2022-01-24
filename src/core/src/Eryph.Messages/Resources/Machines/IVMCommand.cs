using System;

namespace Eryph.Messages.Resources.Machines
{
    public interface IVMCommand
    {
        Guid MachineId { get; set; }
        Guid VMId { get; set; }
    }
}