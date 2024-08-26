using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;

namespace Eryph.Resources.GenePool;

public class GeneData
{
    public GeneIdentifier Id { get; set; }

    public GeneType GeneType { get; set; }

    public long Size { get; set; }

    public string Hash { get; set; }
}
