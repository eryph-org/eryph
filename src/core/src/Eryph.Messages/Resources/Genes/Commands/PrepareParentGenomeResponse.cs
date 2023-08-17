using Eryph.Resources;

namespace Eryph.Messages.Resources.Images.Commands
{
    public class PrepareParentGenomeResponse
    {
        public string RequestedParent { get; set; }

        public string ResolvedParent { get; set; }

    }

    public class PrepareGeneResponse
    {
        public GeneType GeneType { get; set; }
        public string RequestedGene { get; set; }

        public string ResolvedGene { get; set; }

    }
}