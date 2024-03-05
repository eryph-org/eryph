using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications
{
    public static class OperationSpecs
    {
        internal static void ExpandFields(ISpecificationBuilder<OperationModel> query, string expand, DateTimeOffset requestLogTimestamp)
        {
            if (string.IsNullOrWhiteSpace(expand)) return;


            var expandedFields = expand.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var expandedField in expandedFields)
            {
                switch (expandedField)
                {
                    case "logs":
                        query.Include(x => x.LogEntries
                            .Where(l => l.Timestamp > requestLogTimestamp));
                        break;
                    case "tasks":
                        query.Include(x => x.Tasks);
                        break;
                    case "resources":
                        query.Include(x => x.Resources);
                        break;
                    case "projects":
                        query.Include(x => x.Projects).ThenInclude(x=>x.Project);
                        break;
                }
            }
        }


        public sealed class GetAll : Specification<OperationModel>
        {
            public GetAll(AuthContext authContext, IEnumerable<Guid> sufficientRoles, string expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.Where(x=>x.TenantId == authContext.TenantId);

                if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                    // we have to check if the user is authorized for all project in the operation
                    Query.Where(x =>  x.Projects.All(projectRef =>
                        projectRef.Project.ProjectRoles.Any(y => 
                            authContext.Identities.Contains(y.IdentityId) && sufficientRoles.Contains(y.RoleId))));

                Query.OrderBy(x => x.Id);
                ExpandFields(Query, expanded, requestLogTimestamp);

            }
        }

        public sealed class GetById : Specification<OperationModel>, ISingleResultSpecification<OperationModel>
        {
            public GetById(Guid id, AuthContext authContext, IEnumerable<Guid> sufficientRoles, string expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.Where(x => x.Id == id && x.TenantId == authContext.TenantId);

                if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                    // we have to check if the user is authorized for all project in the operation
                    Query.Where(x => x.Projects.All(projectRef =>
                        projectRef.Project.ProjectRoles.Any(y =>
                            authContext.Identities.Contains(y.IdentityId) && sufficientRoles.Contains(y.RoleId))));

                ExpandFields(Query, expanded, requestLogTimestamp);


            }

            public GetById(Guid id, Guid tenantId, string expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.Where(x => x.Id == id && x.TenantId == tenantId);
                ExpandFields(Query, expanded, requestLogTimestamp);


            }
        }

    }
}