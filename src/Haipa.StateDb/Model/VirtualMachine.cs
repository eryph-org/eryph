using System;
using System.Collections.Generic;

namespace Haipa.StateDb.Model
{
    public class VirtualMachine
    {
        public Guid Id { get; set; }
        public Machine Machine { get; set; }
        public string Path { get; set; }
        public virtual List<VirtualMachineNetworkAdapter> NetworkAdapters { get; set; }

    }
}