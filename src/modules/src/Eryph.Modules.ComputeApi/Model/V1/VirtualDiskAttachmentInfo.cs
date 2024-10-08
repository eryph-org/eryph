using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualDiskAttachmentInfo
{
    public required CatletDriveType Type { get; set; }

    public required string CatletId { get; set; }
}
