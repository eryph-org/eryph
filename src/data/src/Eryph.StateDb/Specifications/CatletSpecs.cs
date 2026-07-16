using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public class CatletSpecs
{
    /// <summary>
    /// A catlet by its name within a project and environment. A catlet is identified by all three:
    /// the same name can exist in different environments of one project.
    /// </summary>
    public sealed class GetByName : Specification<Catlet>, ISingleResultSpecification
    {
        public GetByName(string name, Guid tenantId, string projectName, string environment)
        {
            Query
                .Include(x => x.Project)
                .Where(x => x.Project.TenantId == tenantId && x.Project.Name == projectName.ToLowerInvariant())
                .Where(x => x.Environment == environment.ToLowerInvariant())
                .Where(x => x.Name == name.ToLowerInvariant());
        }

        public GetByName(string name, Guid projectId, string environment)
        {
            Query
                .Where(x => x.ProjectId == projectId)
                .Where(x => x.Environment == environment.ToLowerInvariant())
                .Where(x => x.Name == name.ToLowerInvariant());
        }
    }

    public sealed class GetByVmId : Specification<Catlet>, ISingleResultSpecification
    {
        public GetByVmId(Guid vmId)
        {
            Query.Where(x => x.VmId == vmId)
                .Include(x => x.Project);
        }
    }

    public sealed class GetAllVmIds : Specification<Catlet, Guid>
    {
        public GetAllVmIds(string agent)
        {
            Query.Where(c => c.AgentName == agent);
            Query.Select(c => c.VmId);
        }
    }

    public sealed class GetById : Specification<Catlet>, ISingleResultSpecification
    {
        public GetById(Guid id)
        {
            Query.Where(x => x.Id == id)
                .Include(x => x.Project);
        }
    }

    public sealed class GetForConfig : Specification<Catlet>, ISingleResultSpecification
    {
        public GetForConfig(Guid catletId)
        {
            Query.Where(x => x.Id == catletId)
                .Include(x => x.Project)
                .Include(x => x.Drives)
                .ThenInclude(x => x.AttachedDisk)
                .ThenInclude(x => x!.Parent);
        }
    }

    public sealed class GetForDelete : Specification<Catlet>, ISingleResultSpecification
    {
        public GetForDelete(Guid catletId)
        {
            Query.Where(x => x.Id == catletId)
                .Include(x => x.Project)
                .Include(x => x.Drives)
                .ThenInclude(x => x.AttachedDisk);
        }
    }

    /// <summary>
    /// Every deployment of a specification. A specification is project level and deploys into many
    /// environments, so this can return more than one catlet.
    /// </summary>
    public sealed class ListBySpecificationId : Specification<Catlet>
    {
        public ListBySpecificationId(Guid specificationId)
        {
            Query.Where(x => x.SpecificationId == specificationId);
        }
    }

    /// <summary>
    /// The deployments of several specifications at once, so listing them does not query per
    /// specification.
    /// </summary>
    public sealed class ListBySpecificationIds : Specification<Catlet>
    {
        public ListBySpecificationIds(IReadOnlyList<Guid> specificationIds)
        {
            Query.Where(x => x.SpecificationId.HasValue
                             && specificationIds.Contains(x.SpecificationId.Value));
        }
    }

    /// <summary>
    /// The deployment of a specification into one environment. This is the unique one:
    /// a specification deploys at most once per environment.
    /// </summary>
    public sealed class GetBySpecificationIdAndEnvironment : Specification<Catlet>, ISingleResultSpecification
    {
        public GetBySpecificationIdAndEnvironment(Guid specificationId, string environment)
        {
            Query
                .Where(x => x.SpecificationId == specificationId)
                .Where(x => x.Environment == environment.ToLowerInvariant());
        }
    }
}
