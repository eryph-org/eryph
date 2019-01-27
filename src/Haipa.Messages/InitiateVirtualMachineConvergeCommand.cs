using System;
using Haipa.VmConfig;

namespace Haipa.Messages
{
    public class ConvergeVirtualMachineCommand : OperationCommand
    {
        public MachineConfig Config { get; set; }
    }

    public class OperationCommand
    {
        public Guid OperationId { get; set; }

    }

    public interface IOptionalMachineCommand : IMachineCommand
    {

    }

    public interface IMachineCommand 
    {
        Guid MachineId { get; set; }
    }

    public class StartMachineCommand : OperationCommand, IMachineCommand
    {
        public Guid MachineId { get; set; }
    }


    public class StopMachineCommand : OperationCommand, IMachineCommand
    {
        public Guid MachineId { get; set; }
    }

    public class DestroyMachineCommand : OperationCommand, IOptionalMachineCommand
    {
        public Guid MachineId { get; set; }
    }

    public class OperationAcceptedEvent
    {
        public Guid OperationId { get; set; }
        public string AgentName { get; set; }

    }

    public class MachineStateChangedEvent
    {
        public Guid MachineId { get; set; }
        public VmStatus Status { get; set; }
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