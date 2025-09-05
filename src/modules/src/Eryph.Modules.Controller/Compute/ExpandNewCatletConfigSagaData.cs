using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

internal class ExpandNewCatletConfigSagaData
{
    public Architecture Architecture { get; set; }
    
    public CatletConfig? Config { get; set; }

    public bool ShowSecrets { get; set; }

    public CatletConfig? BredConfig { get; set; }

    public string? AgentName { get; set; }

    public ExpandNewCatletConfigSagaState State { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }
}
