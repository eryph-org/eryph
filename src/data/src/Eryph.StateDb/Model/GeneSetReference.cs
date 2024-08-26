using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Model;

public class GeneSetReference
{
    public Guid Id { get; set; }

    public string Organization { get; set; }

    public string Name { get; set; }

    public string Tag { get; set; }

    public GeneSet GeneSet { get; set; }
}
