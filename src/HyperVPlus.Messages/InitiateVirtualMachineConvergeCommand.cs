using System;
using HyperVPlus.VmConfig;

namespace HyperVPlus.Messages
{
    public class InitiateVirtualMachineConvergeCommand
    {
        public ConfigEntry Config { get; set; }
        public Guid ConvergeProcessId { get; set; }
    }

    public class OperationCommand
    {
        public Guid OperationId { get; set; }

    }

    public interface IMachineCommand 
    {
        Guid MachineId { get; set; }
    }

    public class StartVirtualMachineCommand : OperationCommand, IMachineCommand
    {
        public Guid MachineId { get; set; }
    }

    public class StopVirtualMachineCommand : OperationCommand, IMachineCommand
    {
        public Guid MachineId { get; set; }
    }

    public class OperationAcceptedEvent
    {
        public Guid OperationId { get; set; }
        public string AgentName { get; set; }

    }

    public class AcceptedOperation<T> where T : OperationCommand
    {
        public T Command { get; }

        public AcceptedOperation(T command)
        {
            Command = command;
        }
    }

    public class StartOperation
    {
        public string CommandData { get; set; }
        public string CommandType { get; set; }

        public Guid OperationId { get; set; }


        // ReSharper disable once UnusedMember.Global
        public StartOperation()
        {
        }
        public StartOperation(string commandType, string commandData, Guid operationId)
        {
            CommandType = commandType;
            CommandData = commandData;
            OperationId = operationId;
        }
    }

}