using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class CatletSpecificationVersionSpecs
{
    public sealed class GetByIdReadOnly : Specification<CatletSpecificationVersion>, ISingleResultSpecification
    {
        public GetByIdReadOnly(Guid id)
        {
            Query.Where(x => x.Id == id)
                .AsNoTracking();
        }
    }
}
