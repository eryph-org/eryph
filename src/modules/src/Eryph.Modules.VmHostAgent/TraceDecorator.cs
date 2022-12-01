using System;
using System.Threading.Tasks;
using Eryph.VmManagement;
using Eryph.VmManagement.Tracing;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.VmHostAgent;

public class TraceDecorator<T> : IHandleMessages<T>
{
    private readonly IHandleMessages<T> _decoratedHandler;
    private readonly ITracer _tracer;


    public TraceDecorator(IHandleMessages<T> decoratedHandler, ITracer tracer)
    {
        _decoratedHandler = decoratedHandler;
        _tracer = tracer;
    }
    public async Task Handle(T message)
    {
        var context = new TraceContext(_tracer, Guid.NewGuid());
        TraceContextAccessor.TraceContext = context;
        var messageId = MessageContext.Current.Headers["rbs2-msg-id"];

        try
        {
            context.Write(MessageTraceData.FromObject(message, messageId));
            await _decoratedHandler.Handle(message);
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