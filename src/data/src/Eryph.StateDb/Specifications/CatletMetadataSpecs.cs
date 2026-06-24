using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class CatletMetadataSpecs
{
    public sealed class GetByIdReadonly : Specification<CatletMetadata>, ISingleResultSpecification
    {
        public GetByIdReadonly(Guid id)
        {
            Query.Where(x => x.Id == id)
                .AsNoTracking();
        }
    }
}
