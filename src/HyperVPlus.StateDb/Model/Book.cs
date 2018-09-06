using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HyperVPlus.StateDb.Model
{
    public class Operation
    {
        public Guid Id { get; set; }
        public virtual List<OperationLog> LogEntries { get; set; }
        public Guid MachineGuid { get; set; }

        public OperationStatus Status { get; set; }
        public string AgentName { get; set; }
    }

    public class Machine
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public Agent Agent { get; set; }
        public string AgentName { get; set; }

        public MachineStatus Status { get; set; }

        public virtual List<IpV4Address> IpV4Addresses { get; set; }
        public virtual List<IpV6Address> IpV6Addresses { get; set; }
    }

    public class IpV4Address
    {
        public Machine Machine { get; set; }
        public Guid MachineId { get; set; }
        public string Address { get; set; }

    }

    public enum MachineStatus
    {
        Stopped,
        Running
    }

    public enum OperationStatus
    {
        Queued,
        Running,
        Failed,
        Completed,
    }

    public class IpV6Address
    {
        public Machine Machine { get; set; }
        public Guid MachineId { get; set; }

        public string Address { get; set; }

    }

    public class Agent
    {
        public string Name { get; set; }
        public virtual List<Machine> Machines { get; set; }
    }


    public class OperationLog
    {
        public Guid Id { get; set; }
        
        public string Message { get; set; }
        public Operation Operation { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }


}