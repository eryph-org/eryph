using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public class RequestIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "x-ms-client-request-id";

    public async Task Invoke(HttpContext context)
    {
        var correlationId = GetRequestId(context);
        AddRequestIdHeaderToResponse(context, correlationId);
        context.TraceIdentifier = correlationId;

        await next(context);
    }

    private static string GetRequestId(HttpContext context)
    {
        var requestId = context.Request.Headers[CorrelationIdHeader].ToString();
        if (string.IsNullOrWhiteSpace(requestId))
            requestId = Guid.NewGuid().ToString();

        return requestId;
    }

    private static void AddRequestIdHeaderToResponse(HttpContext context, string requestId)
        => context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = requestId;
            return Task.CompletedTask;
        });
}
