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
    public IList<(GeneSetIdentifier Source, GeneSetIdentifier Target)> ResolvedGeneSets { get; set; }

    public IList<(GeneSetIdentifier Id, CatletConfig Config)> ParentConfigs { get; set; }
}
