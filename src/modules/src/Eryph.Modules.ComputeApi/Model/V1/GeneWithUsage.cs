using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class GeneWithUsage : Gene
{
    public IList<Guid> Catlets { get; set; } = null!;

    public IList<Guid> Disks { get; set; } = null!;
}
