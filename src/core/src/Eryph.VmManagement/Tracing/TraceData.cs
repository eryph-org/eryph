using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Eryph.VmManagement;

public abstract class TraceData
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public abstract string Type { get; }
    public Dictionary<string, object> Data { get; init; }

}