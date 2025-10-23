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

        public GetByIdReadOnly(Guid specificationId, Guid id)
        {
            Query.Where(x => x.Id == id && x.SpecificationId == specificationId)
                .AsNoTracking();
        }
    }

    public sealed class GetLatestBySpecificationIdReadOnly : Specification<CatletSpecificationVersion>, ISingleResultSpecification
    {
        public GetLatestBySpecificationIdReadOnly(Guid specificationId)
        {
            Query.Where(x => x.SpecificationId == specificationId)
                .OrderByDescending(x => x.CreatedAt)
                .Include(x => x.Genes)
                .Take(1)
                .AsNoTracking();
        }
    }

    public sealed class ListBySpecificationIdReadOnly : Specification<CatletSpecificationVersion>
    {
        public ListBySpecificationIdReadOnly(Guid specificationId)
        {
            Query.Where(x => x.SpecificationId == specificationId)
                .AsNoTracking();
        }
    }

    public sealed class ListBySpecificationId : Specification<CatletSpecificationVersion>
    {
        public ListBySpecificationId(Guid specificationId)
        {
            Query.Where(x => x.SpecificationId == specificationId);
        }
    }
}
