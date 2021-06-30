using System.Collections.Generic;

namespace Haipa.StateDb.Model
{
    public class Machine : Resource
    {
        public string AgentName { get; set; }


        public MachineStatus Status { get; set; }
        public MachineType MachineType { get; set; }


        public virtual List<MachineNetwork> Networks { get; set; }
    }

}