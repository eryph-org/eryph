using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public class GeneSpecs
{
    public sealed class GetById : Specification<Gene>, ISingleResultSpecification<Gene>
    {
        public GetById(Guid id)
        {
            Query.Where(x => x.Id == id)
                .Include(x => x.GeneSet);
        }
    }

    public sealed class GetAll : Specification<Gene>
    {
        public GetAll()
        {
            Query.Include(x => x.GeneSet);
        }
    }
}
