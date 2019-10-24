using System.Threading.Tasks;
using Haipa.Messages.Operations;

namespace Haipa.Modules.Controller.Operations
{
    internal interface IOperationTaskDispatcher
    {
        Task Send(OperationTaskCommand message);
    }
}