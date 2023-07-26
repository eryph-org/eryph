using Rebus.Pipeline;

namespace Eryph.Rebus;

public static class MessageContextExtensions
{
    public static string GetTraceId(this IMessageContext messageContext)
    {
        return messageContext.Headers.TryGetValue("trace_id", out var id) 
            ? id 
            : "";
    }
}