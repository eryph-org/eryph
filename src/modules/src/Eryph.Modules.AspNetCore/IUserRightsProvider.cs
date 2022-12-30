using System;
using System.Threading.Tasks;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore;

public interface IUserRightsProvider
{
    Guid GetUserTenantId();
    Guid[] GetUserRoles();
    Task<bool> HasResourceAccess(Guid resourceId, AccessRight requiredAccess);
    Task<bool> HasResourceAccess(Resource resource, AccessRight requiredAccess);
    Task<bool> HasProjectAccess(Guid projectId, AccessRight requiredAccess);
    Task<bool> HasProjectAccess(Project project, AccessRight requiredAccess);
}