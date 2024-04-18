using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class CatletMetadataSpecs
{
    public sealed class GetById : Specification<CatletMetadata>, ISingleResultSpecification
    {
        public GetById(Guid id)
        {
            Query.Where(x => x.Id == id);
        }
    }
}
