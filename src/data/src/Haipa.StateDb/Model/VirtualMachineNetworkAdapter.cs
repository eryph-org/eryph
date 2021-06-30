using System;

namespace Haipa.StateDb.Model
{
    public class VirtualMachineNetworkAdapter
    {
        public string Id { get; set; }

        public Guid MachineId { get; set; }
        public VirtualMachine Vm { get; set; }
        public string Name { get; set; }

        public string SwitchName { get; set; }

        public string MacAddress { get; set; }
    }
}