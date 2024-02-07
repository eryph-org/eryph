using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Resources;
using Eryph.StateDb.Model;
using Resource = Eryph.StateDb.Model.Resource;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints;

public class TestingUserRightsProvider : IUserRightsProvider
{
    public string GetUserId()
    {
        return "test";
    }

    public Guid GetUserTenantId()
    {
        return EryphConstants.DefaultTenantId;
    }

    public Guid[] GetUserRoles()
    {
        return new[] { EryphConstants.SuperAdminRole };
    }

    public AuthContext GetAuthContext()
    {
        return new AuthContext(EryphConstants.DefaultTenantId,
            new[] { "test" }, new[]
            {
                EryphConstants.SuperAdminRole
            });
    }

    public Task<bool> HasResourceAccess(Guid resourceId, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }

    public Task<bool> HasResourceAccess(Resource resource, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }

    public Task<bool> HasProjectAccess(string projectName, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }

    public Task<bool> HasProjectAccess(Guid projectId, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }

    public Task<bool> HasProjectAccess(Project project, AccessRight requiredAccess)
    {
        return Task.FromResult(true);
    }

    public IEnumerable<Guid> GetResourceRoles<TResource>(AccessRight accessRight) where TResource : Resource
    {
        return Enumerable.Empty<Guid>();
    }

    public IEnumerable<Guid> GetResourceRoles(ResourceType resourceType, AccessRight accessRight)
    {
        return Enumerable.Empty<Guid>();
    }

    public IEnumerable<Guid> GetProjectRoles(AccessRight accessRight)
    {
        return Enumerable.Empty<Guid>();
    }
}