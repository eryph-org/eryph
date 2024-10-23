using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;

namespace Eryph.GenePool;

public class GeneData
{
    public required UniqueGeneIdentifier Id { get; set; }

    public required long Size { get; set; }

    public required string Hash { get; set; }
}
