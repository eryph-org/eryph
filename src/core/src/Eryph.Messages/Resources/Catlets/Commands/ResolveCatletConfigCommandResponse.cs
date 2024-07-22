using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class ResolveCatletConfigCommandResponse
{
    public IReadOnlyDictionary<GeneSetIdentifier, GeneSetIdentifier> ResolvedGeneSets { get; set; }

    public IReadOnlyDictionary<GeneSetIdentifier, CatletConfig> ParentConfigs { get; set; }
}
