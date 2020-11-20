using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations
{
    internal interface IOperationTaskDispatcher
    {
        Task Send(OperationTaskCommand message);
        Task StartNewOperation(OperationTaskCommand message, long? resourceId, ResourceType? resourceType);
    }
}