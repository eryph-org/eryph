using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Genepool.Genetics;

public class GenePoolApiKey
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Organization { get; set; }

    public required string Secret { get; set; }
}
