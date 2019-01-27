using System;

namespace Haipa.StateDb.Model
{
    public class AgentNetwork
    {
        public Network Network { get; set; }
        public Agent Agent { get; set; }
        public string AgentName { get; set; }
        public Guid NetworkId { get; set; }

        public string VirtualSwitchName { get; set; }
    }
}