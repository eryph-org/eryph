using System;
using System.Collections.Generic;

namespace Haipa.StateDb.Model
{
    public class Machine
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public Agent Agent { get; set; }
        public string AgentName { get; set; }

        
        public MachineStatus Status { get; set; }


        public VirtualMachine VM { get; set; }
        public virtual List<MachineNetwork> Networks { get; set; }
    }
}