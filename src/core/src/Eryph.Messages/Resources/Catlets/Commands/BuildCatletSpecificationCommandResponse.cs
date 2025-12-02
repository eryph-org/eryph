using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class BuildCatletSpecificationCommandResponse
{
    public Architecture Architecture { get; set; }

    public CatletConfig BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }
}
