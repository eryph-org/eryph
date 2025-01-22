using System;
using System.Collections.Generic;
using Eryph.Resources.Disks;

namespace Eryph.Messages.Resources.Disks;

public class CheckDisksExistsReply
{
    public IReadOnlyList<DiskInfo> MissingDisks { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
