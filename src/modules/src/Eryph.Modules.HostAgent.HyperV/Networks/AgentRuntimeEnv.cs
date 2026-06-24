using System;
using System.Threading;

namespace Eryph.Modules.HostAgent.Networks;

public class AgentRuntimeEnv(
    CancellationTokenSource source,
    CancellationToken token,
    IServiceProvider serviceProvider)
{
    public readonly IServiceProvider ServiceProvider = serviceProvider;
    public readonly CancellationTokenSource Source = source;
    public readonly CancellationToken Token = token;

    public AgentRuntimeEnv(CancellationTokenSource source, IServiceProvider serviceProvider)
        : this(source, source.Token, serviceProvider)
    {
    }
}
