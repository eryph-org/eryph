using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public class Machine : Resource
    {
        public string AgentName { get; set; }


        public MachineStatus Status { get; set; }
        public MachineType MachineType { get; set; }


        public virtual ICollection<MachineNetwork> Networks { get; set; }

        public TimeSpan? UpTime { get; set; }


    }

}