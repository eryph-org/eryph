using Eryph.Core.Genetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class Gene
{
    public string Id { get; set; }

    public GeneType GeneType { get; set; }

    public string GeneSet { get; set; }

    public string Name { get; set; }

    public long Size { get; set; }

    public string Hash { get; set; }
}
