using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class CatletSpecificationSpecs
{
    public sealed class GetByIdReadOnly : Specification<CatletSpecification>, ISingleResultSpecification
    {
        public GetByIdReadOnly(Guid id)
        {
            Query.Where(x => x.Id == id)
                .AsNoTracking();
        }
    }

    public sealed class GetByName : Specification<CatletSpecification>, ISingleResultSpecification
    {
        public GetByName(string name, Guid projectId)
        {
            Query.Include(s => s.Project)
                .Where(s => s.ProjectId == projectId)
                .Where(x => x.Name == name.ToLowerInvariant());
        }
    }
}
