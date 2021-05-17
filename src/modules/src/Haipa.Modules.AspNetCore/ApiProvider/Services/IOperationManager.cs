using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Haipa.Messages.Operations.Commands;
using Haipa.StateDb.Model;
using Resource = Haipa.Resources.Resource;

namespace Haipa.Modules.AspNetCore.ApiProvider.Services
{
    public interface IOperationManager
    {
        Task<Operation?> StartNew<T>(Resource resource = default) where T : OperationTaskCommand;

        Task<IEnumerable<Operation>> StartNew<T>([AllowNull] params Resource[] resources)
            where T : OperationTaskCommand;

        Task<Operation?> StartNew(Type operationCommandType, Resource resource = default);
        Task<IEnumerable<Operation>> StartNew(Type operationCommandType, [AllowNull] params Resource[] resources);
        Task<Operation?> StartNew(OperationTaskCommand operationCommand);

        Task<IEnumerable<Operation>> StartNew(OperationTaskCommand taskCommand,
            [AllowNull] params Resource[] resources);
    }
}