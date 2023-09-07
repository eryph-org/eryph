using Eryph.Resources;

namespace Eryph.Messages.Resources.Genes.Commands;

public class PrepareGeneResponse
{
    public GeneType GeneType { get; set; }
    public string RequestedGene { get; set; }

    public string ResolvedGene { get; set; }

}