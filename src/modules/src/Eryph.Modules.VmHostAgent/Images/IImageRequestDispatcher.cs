using Dbosoft.Rebus.Operations;

namespace Eryph.Modules.VmHostAgent.Images;

public interface IImageRequestDispatcher
{
    void NewImageRequestTask(IOperationTaskMessage message, string image);
}