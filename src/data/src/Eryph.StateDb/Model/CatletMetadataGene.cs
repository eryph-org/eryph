using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Model;

public class CatletMetadataGene
{
    public required Guid MetadataId {get; set; }

    public required string GeneSet { get; set; }

    public required string Name { get; set; }

    public required string Architecture { get; set; }

    internal string Combined
    {
        get => StateStoreGeneExtensions.ToIndexed(GeneSet, Name, Architecture);
        private set => _ = value;
    }
}
