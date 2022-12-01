using System;
using System.Collections.Generic;

namespace Eryph.VmManagement.Tracing;

public abstract class TraceData
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public abstract string Type { get; }
    public Dictionary<string, object> Data { get; init; }

}