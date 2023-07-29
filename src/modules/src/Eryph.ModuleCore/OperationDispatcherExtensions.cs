using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;

namespace Eryph.ModuleCore;

public static class OperationDispatcherExtensions
{
    public static ValueTask<IOperation?> StartNew(this IOperationDispatcher dispatcher, Guid tenantId, string traceId, object command)
    {
        return dispatcher.StartNew(command, 
            additionalData:new OperationDataRecord(tenantId, traceId),
            additionalHeaders:new Dictionary<string, string>
            {
                {"trace_id", traceId}
            });
    }
}