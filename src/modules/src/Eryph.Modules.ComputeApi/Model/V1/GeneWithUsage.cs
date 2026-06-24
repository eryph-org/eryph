using System.Collections.Generic;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class GeneWithUsage : Gene
{
    public IReadOnlyList<string>? Catlets { get; set; }

    public IReadOnlyList<string>? Disks { get; set; }
}
