using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;

namespace Eryph.Resources.GenePool;

public class GeneSetData
{
    public GeneSetIdentifier Id { get; set; }

    public IList<GeneData> Genes { get; set; }
}
