using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class OperationSpecs
{
    internal static void ExpandFields(
        ISpecificationBuilder<OperationModel> query,
        string? expand,
        DateTimeOffset? requestLogTimestamp)
    {
        if (string.IsNullOrWhiteSpace(expand))
            return;

        var expandedFields = expand.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var expandedField in expandedFields)
            switch (expandedField)
            {
                case "logs":
                    if (requestLogTimestamp == null)
                        query.Include(x => x.LogEntries);
                    else
                        query.Include(x => x.LogEntries.Where(l => l.Timestamp > requestLogTimestamp));
                    break;
                case "tasks":
                    query.Include(x => x.Tasks)
                        .ThenInclude(x => x.Progress);
                    break;
                case "resources":
                    query.Include(x => x.Resources);
                    break;
                case "projects":
                    query.Include(x => x.Projects).ThenInclude(x => x.Project);
                    break;
            }
    }

    public sealed class GetAll : Specification<OperationModel>
    {
        public GetAll(AuthContext authContext, IEnumerable<Guid> sufficientRoles, string? expanded,
            DateTimeOffset? requestLogTimestamp)
        {
            Query.Where(x => x.TenantId == authContext.TenantId);

            if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                // we have to check if the user is authorized for all project in the operation
                Query.Where(x => x.Projects.All(projectRef =>
                    projectRef.Project.ProjectRoles.Any(y =>
                        authContext.Identities.Contains(y.IdentityId) && sufficientRoles.Contains(y.RoleId))));

            Query.OrderBy(x => x.Id);
            ExpandFields(Query, expanded, requestLogTimestamp);
        }
    }

    /// <summary>
    /// Finds terminal operations (completed, failed or cancelled) whose last update is
    /// older than the given cutoff. Used by housekeeping to delete operations by age;
    /// active operations are never deleted this way, only timed out (see
    /// <see cref="FindTimedOut"/>), regardless of how retention and timeout are configured.
    /// </summary>
    public sealed class FindExpired : Specification<OperationModel>
    {
        public FindExpired(DateTimeOffset cutoff)
        {
            Query.Where(x =>
                x.LastUpdated < cutoff
                && (x.Status == OperationStatus.Completed
                    || x.Status == OperationStatus.Failed
                    || x.Status == OperationStatus.Cancelled));
        }
    }

    /// <summary>
    /// Finds operations which are still queued or running but have not been
    /// updated since the given cutoff. Used by housekeeping to cancel operations
    /// which are stuck (e.g. because their agent died). Includes the tasks so
    /// they can be cancelled together with the operation.
    /// </summary>
    public sealed class FindTimedOut : Specification<OperationModel>
    {
        public FindTimedOut(DateTimeOffset cutoff)
        {
            Query.Where(x =>
                    (x.Status == OperationStatus.Queued || x.Status == OperationStatus.Running)
                    && x.LastUpdated < cutoff)
                .Include(x => x.Tasks);
        }
    }

    /// <summary>
    /// Finds an operation that the caller is allowed to cancel: the operation's
    /// requester, an owner of all the operation's projects, or a super admin.
    /// </summary>
    public sealed class GetByIdForCancellation
        : Specification<OperationModel>, ISingleResultSpecification<OperationModel>
    {
        public GetByIdForCancellation(
            Guid id, AuthContext authContext, string requesterId, IEnumerable<Guid> ownerRoles)
        {
            Query.Where(x => x.Id == id && x.TenantId == authContext.TenantId);

            if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                Query.Where(x =>
                    // The caller requested the operation themselves...
                    x.RequestedBy == requesterId
                    // ...or is an owner of every project the operation touches.
                    || (x.Projects.Any() && x.Projects.All(projectRef =>
                        projectRef.Project.ProjectRoles.Any(y =>
                            authContext.Identities.Contains(y.IdentityId) && ownerRoles.Contains(y.RoleId)))));
        }
    }

    public sealed class GetById : Specification<OperationModel>, ISingleResultSpecification<OperationModel>
    {
        public GetById(Guid id, AuthContext authContext, IEnumerable<Guid> sufficientRoles, string? expanded,
            DateTimeOffset? requestLogTimestamp)
        {
            Query.Where(x => x.Id == id && x.TenantId == authContext.TenantId);

            if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                // we have to check if the user is authorized for all project in the operation
                Query.Where(x => x.Projects.All(projectRef =>
                    projectRef.Project.ProjectRoles.Any(y =>
                        authContext.Identities.Contains(y.IdentityId) && sufficientRoles.Contains(y.RoleId))));

            ExpandFields(Query, expanded, requestLogTimestamp);
        }

        public GetById(Guid id, Guid tenantId, string? expanded, DateTimeOffset requestLogTimestamp)
        {
            Query.Where(x => x.Id == id && x.TenantId == tenantId);
            ExpandFields(Query, expanded, requestLogTimestamp);
        }
    }
}
