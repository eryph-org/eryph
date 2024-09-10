using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Resources.Disks;

namespace Eryph.Messages.Resources.Disks;

public class CreateVirtualDiskVMCommandResponse
{
    public DiskInfo DiskInfo { get; set; }
}
