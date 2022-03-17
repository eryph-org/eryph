using System;

namespace Eryph.Modules.VmHostAgent;

public interface IImageRequestDispatcher
{
    void NewImageRequestTask(Guid operationId, Guid taskId, string image);
}