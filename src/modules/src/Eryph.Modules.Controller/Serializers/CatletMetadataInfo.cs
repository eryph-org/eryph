using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Serializers;

public class CatletMetadataInfo
{
    public Guid Id { get; set; }

    public Guid CatletId { get; set; }

    public Guid VmId { get; set; }
}
