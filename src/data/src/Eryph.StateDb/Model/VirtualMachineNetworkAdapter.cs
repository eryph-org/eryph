using System;

namespace Eryph.StateDb.Model
{
    public class VirtualMachineNetworkAdapter
    {
        public string Id { get; set; }

        public Guid MachineId { get; set; }
        public virtual VirtualMachine Vm { get; set; }
        public string Name { get; set; }

        public string SwitchName { get; set; }

        public string MacAddress { get; set; }
    }
}