using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Model;

public class CatletSpecification : Resource
{
    public Guid LatestId { get; set; }

    // TODO the Latest causes a circular reference and saving fails
    // (at least when saving spec and version together)
    // not saving together might cause issues during seeding ->
    // seeding would save spec config json without latest as the change tracking
    // saves it during seeding
    public CatletSpecificationVersion Latest { get; set; } = null!;

    public ICollection<CatletSpecificationVersion> Versions { get; set; } = null!;


}
