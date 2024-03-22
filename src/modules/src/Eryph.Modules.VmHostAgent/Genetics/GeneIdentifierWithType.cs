using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal record GeneIdentifierWithType(GeneType GeneType, GeneIdentifier GeneIdentifier)
{
    public readonly GeneType GeneType = GeneType;
    public readonly GeneIdentifier GeneIdentifier = GeneIdentifier;
}
