using System;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.StateDb.Model;

namespace Haipa.Modules.Api.Services
{
    public interface IOperationManager
    {
        Task<Operation> StartNew<T>(Guid vmId) where T : OperationCommand;
        Task<Operation> StartNew(Type operationCommandType, Guid vmId);
        Task<Operation> StartNew(OperationCommand operationCommand, Guid vmId);

        Task<Operation> StartNew<T>() where T : OperationCommand;
        Task<Operation> StartNew(Type operationCommandType);
        Task<Operation> StartNew(OperationCommand operationCommand);

    }
}