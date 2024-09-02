using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Model;

public class GeneSetReference
{
    public Guid Id { get; set; }

    public required string GeneSet { get; set; }

    public IList<Gene> Genes { get; set; }
}
