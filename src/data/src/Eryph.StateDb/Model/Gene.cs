using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;

namespace Eryph.StateDb.Model;

public class Gene
{
    public Guid Id { get; set; }

    public GeneType GeneType { get; set; }

    public string Name { get; set; }

    public Guid GeneSetId { get; set; }

    public GeneSet GeneSet { get; set; }

    public DateTimeOffset LastSeen { get; set; }

    public long Size { get; set; }

    public string Hash { get; set; }
}