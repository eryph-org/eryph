using System;
using System.Collections.Generic;

namespace Haipa.StateDb.Model
{
    public class Operation
    {
        public Guid Id { get; set; }
        public virtual List<OperationLog> LogEntries { get; set; }
        public Guid MachineGuid { get; set; }

        public OperationStatus Status { get; set; }
        public string AgentName { get; set; }
        public string StatusMessage { get; set; }
        public string Name { get; set; }
    }
}