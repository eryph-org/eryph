using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.GenePool;

namespace Eryph.Messages.Resources.CatletSpecifications;

public class BuildCatletSpecificationCommandResponse
{
    public CatletConfig BuiltConfig { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; } = [];

    public DateTimeOffset Timestamp { get; set; }

    public IReadOnlyList<GeneData> Inventory = [];
}