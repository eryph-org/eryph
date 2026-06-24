using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class PrepareNewCatletConfigCommandResponse
{
    public CatletConfig? ResolvedConfig { get; set; }

    public CatletConfig? ParentConfig { get; set; }

    public CatletConfig? BredConfig { get; set; }

    public string? AgentName { get; set; }

    public Architecture? Architecture { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier>? ResolvedGenes { get; set; }
}
