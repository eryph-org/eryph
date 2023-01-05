using System;
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
            public GetAll(
                
                Guid tenantId, Guid[] roles, AccessRight requiredAccess, [CanBeNull] string filteredProject,
                Action<ISpecificationBuilder<T>> customizeAction = null)
            {

                Query.Where(x => x.Project.TenantId == tenantId);

                if (filteredProject!= null)
                {
                    if(Guid.TryParse(filteredProject, out var projectId))
                        Query.Where(x => x.ProjectId == projectId);
                    else
                        Query.Where(x => x.Project.Name == filteredProject);

                }


                if (!roles.Contains(EryphConstants.SuperAdminRole))
                    Query.Where(x => x.Project.Roles.Any(y=> roles.Contains(y.RoleId) && y.AccessRight>= requiredAccess) );



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

            public GetById(Guid id,
                Guid tenantId, Guid[] roles, AccessRight requiredAccess,
                Action<ISpecificationBuilder<T>> customizeAction = null)
            {

                Query.Where(x => x.Id == id && x.Project.TenantId == tenantId)
                    .Include(x => x.Project);


                if (!roles.Contains(EryphConstants.SuperAdminRole))
                    Query.Where(x => x.Project.Roles.Any(y => roles.Contains(y.RoleId) 
                                                              && y.AccessRight >= requiredAccess));

                customizeAction?.Invoke(Query);

            }
        }

    }




}