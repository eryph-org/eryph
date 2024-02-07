using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;
using JetBrains.Annotations;

namespace Eryph.StateDb.Specifications
{


    public static class ResourceSpecs<T> where T: Resource
    {

        public sealed class GetAll : Specification<T>
        {
            public GetAll(AuthContext authContext, IEnumerable<Guid> sufficientRoles, [CanBeNull] string filteredProject,
                Action<ISpecificationBuilder<T>> customizeAction = null)
            {

                Query.Where(x => x.Project.TenantId == authContext.TenantId);

                if (filteredProject!= null)
                {
                    if(Guid.TryParse(filteredProject, out var projectId))
                        Query.Where(x => x.ProjectId == projectId);
                    else
                        Query.Where(x => x.Project.Name == filteredProject);

                }
                
                if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                    Query.Where(x => x.Project.ProjectRoles
                        .Any(y=> authContext.Identities.Contains(y.IdentityId) 
                                 && sufficientRoles.Contains(y.RoleId)));



                Query.OrderBy(x => x.Id);
                customizeAction?.Invoke(Query);

            }
        }

        public sealed class GetAllForProject : Specification<T>
        {
            public GetAllForProject(Guid tenantId, string projectName,  Action<ISpecificationBuilder<T>> customizeAction = null)
            {
                Query
                    .Where(x=>x.Project.TenantId == tenantId && x.Project.Name == projectName)
                    .OrderBy(x => x.Id);
                customizeAction?.Invoke(Query);

            }
        }

        public sealed class GetByName : Specification<T>
        {
            public GetByName(string name, Action<ISpecificationBuilder<T>> customizeAction = null)
            {
                Query.Where(x => x.Name == name);
                customizeAction?.Invoke(Query);
            }
        }

        public sealed class GetById : Specification<T>, ISingleResultSpecification<T>
        {
            public GetById(Guid id, Action<ISpecificationBuilder<T>> customizeAction = null)
            {
                Query.Where(x => x.Id == id).Include(x => x.Project);
                customizeAction?.Invoke(Query);

            }

            public GetById(Guid id, AuthContext authContext, IEnumerable<Guid> sufficientRoles,
                Action<ISpecificationBuilder<T>> customizeAction = null)
            {

                Query.Where(x => x.Id == id && x.Project.TenantId == authContext.TenantId)
                    .Include(x => x.Project);


                if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
                    Query.Where(x => x.Project.ProjectRoles
                        .Any(y => authContext.Identities.Contains(y.IdentityId)
                                  && sufficientRoles.Contains(y.RoleId)));

                customizeAction?.Invoke(Query);

            }
        }

    }




}