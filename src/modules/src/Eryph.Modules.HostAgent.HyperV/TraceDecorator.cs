using System;
using System.Threading.Tasks;
using Eryph.Rebus;
using Eryph.VmManagement.Tracing;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.HostAgent;

public class TraceDecorator<T>(IHandleMessages<T> decoratedHandler, ITracer tracer) : IHandleMessages<T>
{
    public async Task Handle(T message)
    {
        var correlationId = MessageContext.Current.GetTraceId();

        var context = new TraceContext(tracer, Guid.NewGuid(), correlationId);
        TraceContextAccessor.TraceContext = context;
        var messageId = MessageContext.Current.Headers["rbs2-msg-id"];

        try
        {
            context.Write(MessageTraceData.FromObject(message, messageId));
            await decoratedHandler.Handle(message);
        }
        catch (Exception ex)
        {
            context.Write(ExceptionTraceData.FromException(ex));
            throw;
        }
        finally
        {
            context.Dispose();
            TraceContextAccessor.TraceContext = TraceContext.Empty;
        }
    }
}
