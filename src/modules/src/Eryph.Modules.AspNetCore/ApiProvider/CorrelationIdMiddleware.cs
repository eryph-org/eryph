using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "x-ms-client-request-id";

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context, ICorrelationIdGenerator correlationIdGenerator)
    {
        var correlationId = GetCorrelationId(context, correlationIdGenerator);
        AddCorrelationIdHeaderToResponse(context, correlationId);
        context.TraceIdentifier = correlationId;

        await _next(context);
    }

    private static StringValues GetCorrelationId(HttpContext context, ICorrelationIdGenerator correlationIdGenerator)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            correlationIdGenerator.Set(correlationId);
            return correlationId;
        }
        else
        {
            return correlationIdGenerator.Get();
        }
    }

    private static void AddCorrelationIdHeaderToResponse(HttpContext context, StringValues correlationId)
        => context.Response.OnStarting(() =>
        {
            context.Response.Headers.Add(CorrelationIdHeader, new[] { correlationId.ToString() });
            return Task.CompletedTask;
        });
}