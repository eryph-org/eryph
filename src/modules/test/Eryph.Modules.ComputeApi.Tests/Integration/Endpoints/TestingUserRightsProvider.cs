using System;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints;

public class TestingUserRightsProvider : IUserRightsProvider
{
    public Guid GetUserTenantId()
    {
        return EryphConstants.DefaultTenantId;
    }

    public Guid[] GetUserRoles()
    {
        return new[] { EryphConstants.SuperAdminRole };
    }

    public Task<bool> HasResourceAccess(Guid resourceId, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }

    public Task<bool> HasResourceAccess(Resource resource, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }

    public Task<bool> HasProjectAccess(Guid projectId, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }

    public Task<bool> HasProjectAccess(StateDb.Model.Project project, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }
}