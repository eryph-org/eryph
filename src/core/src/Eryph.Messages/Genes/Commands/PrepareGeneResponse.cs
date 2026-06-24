using System;
using Eryph.Core.Genetics;
using Eryph.GenePool;

namespace Eryph.Messages.Genes.Commands;

public class PrepareGeneResponse
{
    public UniqueGeneIdentifier? RequestedGene { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public GeneData? Inventory { get; set; }
}
