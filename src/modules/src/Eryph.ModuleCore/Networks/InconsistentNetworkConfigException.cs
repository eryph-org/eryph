using System;

namespace Eryph.ModuleCore.Networks;

public class InconsistentNetworkConfigException : Exception
{
    public InconsistentNetworkConfigException()
    {
    }

    public InconsistentNetworkConfigException(string message) : base(message)
    {
    }

    public InconsistentNetworkConfigException(string message, Exception inner) : base(message, inner)
    {
    }
}
