using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Model;

public class CatletMetadataGene
{
    public required Guid MetadataId {get; set; }

    public required string GeneId { get; set; }

    public required string Architecture { get; set; }
}
