using System.Collections.Generic;

namespace Haipa.StateDb.Model
{
    public class Agent
    {
        public string Name { get; set; }
        public virtual List<Machine> Machines { get; set; }
        public virtual List<AgentNetwork> Networks { get; set; }
    }
}