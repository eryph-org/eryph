using System;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.GenePool;
using Eryph.GenePool.Model;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Genes.Commands;

public class PrepareGeneResponse
{
    public GeneIdentifierWithType RequestedGene { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public GeneData Inventory { get; set; }
}
