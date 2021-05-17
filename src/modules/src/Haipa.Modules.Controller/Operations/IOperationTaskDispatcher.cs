using System.Threading.Tasks;
using Haipa.Messages.Operations.Commands;

namespace Haipa.Modules.Controller.Operations
{
    internal interface IOperationTaskDispatcher
    {
        Task Send(OperationTaskCommand message);
    }
}