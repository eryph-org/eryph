using System;
using System.Linq;
using Ardalis.Specification;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.StateDb.Specifications;

public static class CatletSpecificationVersionSpecs
{
    public sealed class GetByIdReadOnly : Specification<CatletSpecificationVersion>, ISingleResultSpecification
    {
        public GetByIdReadOnly(Guid id)
        {
            Query.Where(x => x.Id == id)
                .Include(x => x.Variants)
                .ThenInclude(x => x.PinnedGenes.OrderBy(g => g.GeneSet).ThenBy(g => g.Name).ThenBy(g => g.Architecture))
                .AsNoTracking();
        }

        public GetByIdReadOnly(Guid specificationId, Guid id)
        {
            Query.Where(x => x.Id == id && x.SpecificationId == specificationId)
                .Include(x => x.Variants)
                .ThenInclude(x => x.PinnedGenes.OrderBy(g => g.GeneSet).ThenBy(g => g.Name).ThenBy(g => g.Architecture))
                .AsNoTracking();
        }
    }

    public sealed class GetLatestBySpecificationIdReadOnly : Specification<CatletSpecificationVersion>, ISingleResultSpecification
    {
        public GetLatestBySpecificationIdReadOnly(Guid specificationId)
        {
            Query.Where(x => x.SpecificationId == specificationId)
                .OrderByDescending(x => x.CreatedAt)
                .Include(x => x.Variants).ThenInclude(x => x.PinnedGenes)
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
