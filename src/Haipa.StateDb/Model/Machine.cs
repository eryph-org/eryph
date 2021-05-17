using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.VmConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        public MachineType MachineType { get; set; }


        public virtual List<MachineNetwork> Networks { get; set; }
    }

    public class VMHostMachine : Machine
    {
        public VMHostMachine()
        {
            MachineType = MachineType.VMHost;
        }

        public virtual List<VirtualMachine> VMs { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum MachineType
    {
        VMHost,
        VM
    }
}