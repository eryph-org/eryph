using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Tracing;

namespace Eryph.Modules.VmHostAgent.Tracing;

/// <summary>
/// This implementation of <see cref="ITracer"/> does nothing and
/// is used when the tracing is disabled.
/// </summary>
internal class NullTracer : ITracer
{
    public void CloseTrace(Guid traceContext)
    {
    }

    public void Write(Guid contextId, string correlationId, TraceData data, string message = null)
    {
    }
}
