using System;

namespace Eryph.Modules.VmHostAgent.Images;

public interface IImageRequestDispatcher
{
    void NewImageRequestTask(Guid operationId, Guid taskId, string image);
}