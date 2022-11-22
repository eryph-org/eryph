using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent;

public class AgentControlService : IAgentControlService
{
    //use this only for singletons as objects will never be garbage collected
    public ConcurrentDictionary<object, Func<AgentControlEvent, CancellationToken, Task<bool>>> Recipients = new();

    public async Task<bool> SendControlEvent(
        AgentService service, 
        AgentServiceOperation operation,
        CancellationToken cancellationToken)
    {
        var e = new AgentControlEvent
        {
            Service = service, RequestedOperation = operation
        };

        foreach(var recipient in Recipients)
        {
            var res = await recipient.Value(e, cancellationToken);

            if (res)
                return true;
        }

        return false;
    }

    public void Register(object recipient, Func<AgentControlEvent, CancellationToken, Task<bool>> handlerFunc)
    {
        Recipients.TryAdd(recipient, handlerFunc);
    }

    public void UnRegister(object recipient)
    {
        if(Recipients.ContainsKey(recipient))
            Recipients.TryRemove(recipient, out _);
    }
}