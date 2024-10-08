using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class Gene
{
    public required string Id { get; set; }

    public required GeneType GeneType { get; set; }

    public required string GeneSet { get; set; }

    public required string Name { get; set; }

    public required long Size { get; set; }

    public required string Hash { get; set; }
}
