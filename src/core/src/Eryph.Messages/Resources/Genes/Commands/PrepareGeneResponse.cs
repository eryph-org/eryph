using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using Eryph.Genetics;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Genes.Commands;

public class PrepareGeneResponse
{
    public GeneIdentifierWithType RequestedGene { get; set; }

    public GeneIdentifierWithType ResolvedGene { get; set; }
}
