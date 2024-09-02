using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Resources;
using Eryph.StateDb.Model;
using Resource = Eryph.StateDb.Model.Resource;

namespace Eryph.Modules.AspNetCore;

public interface IUserInfoProvider
{
    string GetUserId();

    Guid GetUserTenantId();
    Guid[] GetUserRoles();

    AuthContext GetAuthContext();

}

public interface IUserRightsProvider : IUserInfoProvider
{

    Task<bool> HasResourceAccess(Guid resourceId, AccessRight requiredAccess);
    Task<bool> HasResourceAccess(Resource resource, AccessRight requiredAccess);
    Task<bool> HasProjectAccess(string projectName, AccessRight requiredAccess);
    Task<bool> HasProjectAccess(Guid projectId, AccessRight requiredAccess);
    Task<bool> HasProjectAccess(Project project, AccessRight requiredAccess);
    Task<bool> HasDefaultTenantAccess(AccessRight requiredAccess);
    IEnumerable<Guid> GetResourceRoles<TResource>(AccessRight accessRight) 
        where TResource : Resource;

    IEnumerable<Guid> GetResourceRoles(ResourceType resourceType,
        AccessRight accessRight);

    IEnumerable<Guid> GetProjectRoles(AccessRight accessRight);
}