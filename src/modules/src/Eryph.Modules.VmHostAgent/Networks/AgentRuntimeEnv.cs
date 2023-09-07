using System;
using System.Threading;

namespace Eryph.Modules.VmHostAgent.Networks;

public class AgentRuntimeEnv
{
    public readonly CancellationTokenSource Source;
    public readonly CancellationToken Token;
    public readonly IServiceProvider ServiceProvider;

    public AgentRuntimeEnv(CancellationTokenSource source, CancellationToken token, 
        IServiceProvider serviceProvider)
    {
        Source = source;
        Token = token;
        ServiceProvider = serviceProvider;
    }

    public AgentRuntimeEnv(CancellationTokenSource source, IServiceProvider serviceProvider) 
        : this(source, source.Token, serviceProvider)
    {
    }
}