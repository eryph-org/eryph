using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eryph.StateDb.Model;

public class GeneSet
{
    public Guid Id { get; set; }

    public required string Organization { get; set; }

    public required string Name { get; set; }

    public required string Tag { get; set; }

    public required string Hash { get; set; }

    public required DateTimeOffset LastSeen { get; set; }

    public List<Gene> Genes { get; set; } = null!;

    public List<GeneSetReference> References { get; set; }
}
