using System;
using System.Threading;

namespace Eryph.VmManagement;

public class TraceContextAccessor
{
    private static readonly AsyncLocal<TraceContextHolder> TraceContextCurrent = new();

    public static TraceContext TraceContext
    {
        get => TraceContextCurrent.Value?.Context ?? TraceContext.Empty;
        set
        {
            var holder = TraceContextCurrent.Value;
            if (holder != null)
            {
                // Clear current HttpContext trapped in the AsyncLocals, as its done.
                holder.Context = null;
            }

            if (value.ContextId != Guid.Empty)
            {
                // Use an object indirection to hold the HttpContext in the AsyncLocal,
                // so it can be cleared in all ExecutionContexts when its cleared.
                TraceContextCurrent.Value = new TraceContextHolder { Context = value };
            }
        }
    }

    private class TraceContextHolder
    {
        public TraceContext? Context;
    }
}