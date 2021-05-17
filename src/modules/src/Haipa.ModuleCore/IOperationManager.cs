using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.Messages.Operations.Commands;
using Haipa.StateDb.Model;
using Resource = Haipa.Primitives.Resources.Resource;

namespace Haipa.ModuleCore
{
    public interface IOperationManager
    {
        Task<Operation?> StartNew<T>() where T : OperationTaskCommand;
        Task<Operation?> StartNew<T>(Resource resource) where T : OperationTaskCommand;
        Task<IEnumerable<Operation>> StartNew<T>(params Resource[] resources) where T : OperationTaskCommand;
        Task<Operation?> StartNew(Type operationCommandType);
        Task<Operation?> StartNew(Type operationCommandType, Resource resource);
        Task<IEnumerable<Operation>> StartNew(Type operationCommandType, params Resource[] resources);
        Task<Operation?> StartNew(OperationTaskCommand operationCommand);
        Task<IEnumerable<Operation>> StartNew(OperationTaskCommand taskCommand, params Resource[] resources);
    }
}