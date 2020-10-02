using System;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.StateDb.Model;

namespace Haipa.Modules.ComputeApi.Services
{
    public interface IOperationManager
    {
        Task<Operation> StartNew<T>(Guid vmId) where T : OperationTaskCommand;
        Task<Operation> StartNew(Type operationCommandType, Guid vmId);
        Task<Operation> StartNew(OperationTaskCommand operationCommand, Guid vmId);

        Task<Operation> StartNew<T>() where T : OperationTaskCommand;
        Task<Operation> StartNew(Type operationCommandType);
        Task<Operation> StartNew(OperationTaskCommand operationCommand);

    }
}