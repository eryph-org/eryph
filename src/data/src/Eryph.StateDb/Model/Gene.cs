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

    public required GeneType GeneType { get; set; }

    public required string GeneId { get; set; }

    public required DateTimeOffset LastSeen { get; set; }

    public required string LastSeenAgent { get; set; }

    public required long Size { get; set; }

    public required string Hash { get; set; }
}
