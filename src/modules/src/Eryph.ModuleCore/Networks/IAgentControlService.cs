using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.Networks;

public interface IAgentControlService
{
    Task<bool> SendControlEvent(
        AgentService service,
        AgentServiceOperation operation,
        CancellationToken cancellationToken);

    public void Register(object recipient, Func<AgentControlEvent, CancellationToken, Task<bool>> handlerFunc);
    public void UnRegister(object recipient);

}