using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class ExpandNewCatletConfigSagaData
{
    public CatletConfig? Config { get; set; }

    public bool ShowSecrets { get; set; }

    public CatletConfig? BredConfig { get; set; }

    public string? AgentName { get; set; }

    public ExpandNewCatletConfigSagaState State { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; } = [];

    public IReadOnlyList<UniqueGeneIdentifier> PendingGenes { get; set; } = [];
}
