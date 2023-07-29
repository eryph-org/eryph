using System;
using System.Linq;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.Workflows;

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
            public GetAll(Guid tenantId, Guid[] roles, AccessRight requiredAccess, string expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.Where(x=>x.TenantId == tenantId);

                if (!roles.Contains(EryphConstants.SuperAdminRole))
                    Query.Where(x => x.Projects.Any(projectRef =>
                        projectRef.Project.Roles.Any(y => roles.Contains(y.RoleId) && y.AccessRight >= requiredAccess)));


                Query.OrderBy(x => x.Id);
                ExpandFields(Query, expanded, requestLogTimestamp);

            }
        }

        public sealed class GetById : Specification<OperationModel>, ISingleResultSpecification<OperationModel>
        {
            public GetById(Guid id, Guid tenantId, Guid[] roles, AccessRight requiredAccess,  string expanded, DateTimeOffset requestLogTimestamp)
            {
                Query.Where(x => x.Id == id &&
                                 x.Projects.Any(project => project.Project.TenantId == tenantId));

                if (!roles.Contains(EryphConstants.SuperAdminRole))
                    Query.Where(x => x.Projects.Any(projectRef=> 
                        projectRef.Project.Roles.Any(y => roles.Contains(y.RoleId) && y.AccessRight >= requiredAccess)));

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