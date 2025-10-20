using Ardalis.Specification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model;

public class CatletSpecificationSpecBuilder(
    IUserRightsProvider userRightsProvider)
    : ResourceSpecBuilder<CatletSpecification>(userRightsProvider)
{
    protected override void CustomizeQuery(ISpecificationBuilder<CatletSpecification> query)
    {
        // Limiting the number of results with Take(1) is not supported by Sqlite.
        query.Include(x => x.Versions);
    }
}
