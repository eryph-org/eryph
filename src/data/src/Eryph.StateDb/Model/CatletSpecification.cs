using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Model;

public class CatletSpecification : Resource
{
    public Guid LatestId { get; set; }

    public required CatletSpecificationVersion Latest { get; set; }
}
