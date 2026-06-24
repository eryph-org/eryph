using System.Collections.Generic;
using Rebus.Pipeline;

namespace Eryph.Rebus;

public static class MessageContextExtensions
{
    public static string GetTraceId(this IMessageContext messageContext)
    {
        return messageContext.Headers.GetValueOrDefault("trace_id", "");
    }
}
