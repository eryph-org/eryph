using System;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.StateDb.Model;
using Haipa.VmConfig;

namespace Haipa.Modules.AspNetCore.ApiProvider.Services
{
    public interface IOperationManager
    {
        Task<Operation> StartNew<T>(long? resourceId, ResourceType? resourceType) where T : OperationTaskCommand;
        Task<Operation> StartNew(Type operationCommandType, long? resourceId, ResourceType? resourceType);
        Task<Operation> StartNew(OperationTaskCommand operationCommand, long? resourceId, ResourceType? resourceType);

        Task<Operation> StartNew<T>() where T : OperationTaskCommand;
        Task<Operation> StartNew(Type operationCommandType);
        Task<Operation> StartNew(OperationTaskCommand operationCommand);

    }
}