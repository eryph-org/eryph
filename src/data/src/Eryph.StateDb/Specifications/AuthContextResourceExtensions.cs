using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class AuthContextResourceExtensions
{
    public static ISpecificationBuilder<T> QueryResourceAccess<T>(this AuthContext authContext,
        ISpecificationBuilder<T> query,
        IEnumerable<Guid> sufficientRoles) where T : Resource
    {
        query.Where(x=>x.Project.TenantId == authContext.TenantId)
            .Include(x => x.Project);


        if (!authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
            query.Where(x => x.Project.ProjectRoles
                .Any(y => authContext.Identities.Contains(y.IdentityId)
                          && sufficientRoles.Contains(y.RoleId)));

        return query;
    }
}