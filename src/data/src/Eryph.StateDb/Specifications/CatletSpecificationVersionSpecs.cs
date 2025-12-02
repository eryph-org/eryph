using System;
using System.Linq;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class CatletSpecificationVersionSpecs
{
    public sealed class GetByIdReadOnly : Specification<CatletSpecificationVersion>, ISingleResultSpecification
    {
        public GetByIdReadOnly(Guid id)
        {
            Query.Where(v => v.Id == id)
                .Include(v => v.Variants)
                .ThenInclude(v => v.PinnedGenes
                    .OrderBy(g => g.GeneType)
                    .ThenBy(g => g.GeneSet)
                    .ThenBy(g => g.Name)
                    .ThenBy(g => g.Architecture))
                .AsNoTracking();
        }

        public GetByIdReadOnly(Guid specificationId, Guid id)
        {
            Query.Where(v => v.Id == id && v.SpecificationId == specificationId)
                .Include(v => v.Variants)
                .ThenInclude(v => v.PinnedGenes
                    .OrderBy(g => g.GeneType)
                    .ThenBy(g => g.GeneSet)
                    .ThenBy(g => g.Name)
                    .ThenBy(g => g.Architecture))
                .AsNoTracking();
        }
    }

    public sealed class ListBySpecificationIdReadOnly : Specification<CatletSpecificationVersion>
    {
        public ListBySpecificationIdReadOnly(Guid specificationId)
        {
            Query.Where(v => v.SpecificationId == specificationId)
                .OrderBy(v => v.CreatedAt)
                .AsNoTracking();
        }
    }

    public sealed class ListBySpecificationId : Specification<CatletSpecificationVersion>
    {
        public ListBySpecificationId(Guid specificationId)
        {
            Query.Where(v => v.SpecificationId == specificationId)
                .OrderBy(v => v.CreatedAt);
        }
    }
}
