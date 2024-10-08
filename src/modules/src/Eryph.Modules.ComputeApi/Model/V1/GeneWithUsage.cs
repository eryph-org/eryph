using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class GeneWithUsage : Gene
{
    public IReadOnlyList<string>? Catlets { get; set; }

    public IReadOnlyList<string>? Disks { get; set; }
}
