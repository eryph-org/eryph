using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualDiskGeneInfo
{
    public required string GeneSet { get; set; }

    public required string Name { get; set; }

    public required string Architecture { get; set; }
}
