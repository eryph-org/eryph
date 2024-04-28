using Eryph.Resources.Disks;

namespace Eryph.Messages.Resources.Disks;

public class CheckDisksExistsReply
{
    public DiskInfo[] MissingDisks { get; set; }
}