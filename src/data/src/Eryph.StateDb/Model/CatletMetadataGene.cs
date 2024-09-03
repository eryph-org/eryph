using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Model;

public class CatletMetadataGene
{
    public Guid Id { get; set; }

    public Guid MetadataId {get; set; }

    public CatletMetadata Metadata { get; set; }

    public required string GeneSet {get; set; }

    public required string GeneName { get; set; }
}
