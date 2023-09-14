using System;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore;

public interface IUserInfoProvider
{
    Guid GetUserTenantId();
    Guid[] GetUserRoles();
}

public interface IUserRightsProvider : IUserInfoProvider
{

    Task<bool> HasResourceAccess(Guid resourceId, AccessRight requiredAccess);
    Task<bool> HasResourceAccess(Resource resource, AccessRight requiredAccess);
    Task<bool> HasProjectAccess(Guid projectId, AccessRight requiredAccess);
    Task<bool> HasProjectAccess(Project project, AccessRight requiredAccess);
}