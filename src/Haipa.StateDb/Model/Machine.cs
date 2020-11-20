using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.VmConfig;

namespace Haipa.StateDb.Model
{
    public class Resource
    {
        [Key]
        public long Id { get; set; }
        public ResourceType ResourceType { get; set; }

        public string Name { get; set; }


    }

    public class Machine : Resource
    {

        public string AgentName { get; set; }


        public MachineStatus Status { get; set; }

        public virtual List<MachineNetwork> Networks { get; set; }
    }

    public class VMHostMachine : Machine
    {

        public virtual List<VirtualMachine> VMs { get; set; }
    }
}