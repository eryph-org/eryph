using System;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class CatletStateResponse
{
    public VmStatus Status { get; set; }

    public TimeSpan UpTime { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
