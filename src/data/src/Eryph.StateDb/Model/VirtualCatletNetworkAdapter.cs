using System;

namespace Eryph.StateDb.Model
{
    public class VirtualCatletNetworkAdapter
    {
        public string Id { get; set; }

        public Guid MachineId { get; set; }
        public virtual VirtualCatlet Vm { get; set; }
        public string Name { get; set; }

        public string SwitchName { get; set; }
        public string NetworkProviderName { get; set; }

        public string MacAddress { get; set; }

    }
}