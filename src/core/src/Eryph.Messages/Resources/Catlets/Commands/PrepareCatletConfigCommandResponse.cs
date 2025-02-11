using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class PrepareCatletConfigCommandResponse
{
    public CatletConfig Config { get; set; }

    public CatletConfig BredConfig { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; }
}
