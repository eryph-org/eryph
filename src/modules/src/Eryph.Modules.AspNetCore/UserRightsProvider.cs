using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Http;
using Resource = Eryph.StateDb.Model.Resource;

namespace Eryph.Modules.AspNetCore;

public class UserRightsProvider : UserInfoProvider, IUserRightsProvider
{
    private readonly IStateStore _stateStore;
    public UserRightsProvider(IHttpContextAccessor contextAccessor, IStateStore stateStore): base(contextAccessor)
    {
        _stateStore = stateStore;
    }


    public async Task<bool> HasResourceAccess(Guid resourceId, AccessRight requiredAccess)
    {
        var resource = await _stateStore.For<Resource>().GetBySpecAsync(new ResourceSpecs<Resource>.GetById(resourceId,
            q => q.Include(x => x.Project).ThenInclude(x => x.ProjectRoles)));
        
        if(resource == null) return false;

        return await HasResourceAccess(resource, requiredAccess);
    }

    public async Task<bool> HasResourceAccess(Resource resource, AccessRight requiredAccess)
    {
        if (resource.Project == null)
            await _stateStore.LoadPropertyAsync(resource, x => x.Project);

        if (resource.Project == null)
            return false;

        return await HasProjectAccess(resource.Project, requiredAccess);

    }

    public async Task<bool> HasProjectAccess(string projectName, AccessRight requiredAccess)
    {
        var tenantId = GetUserTenantId();
        var project = await _stateStore.For<Project>().GetBySpecAsync(
            new ProjectSpecs.GetByName(tenantId, projectName));
        if (project == null) return false;
        return await HasProjectAccess(project, requiredAccess);
    }

    public async Task<bool> HasProjectAccess(Guid projectId, AccessRight requiredAccess)
    {
        var project = await _stateStore.For<Project>().GetByIdAsync(projectId);
        
        if(project == null) return false;
        
        return await HasProjectAccess(project, requiredAccess);
    }

    public async Task<bool> HasProjectAccess(Project project, AccessRight requiredAccess)
    {
        var authContext = GetAuthContext();
        if (authContext.TenantId != project.TenantId)
            return false;

        if (authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
            return true;
        
        var sufficientRoles = GetProjectRoles(requiredAccess);

        var userRoleAssignments = await _stateStore.For<ProjectRoleAssignment>()
            .ListAsync(
                new ProjectRoleAssignmentSpecs.GetByProject(project.Id, authContext.Identities));
        
        return userRoleAssignments.Any(x => sufficientRoles.Contains(x.RoleId));
    }

    public Task<bool> HasDefaultTenantAccess(AccessRight requiredAccess)
    {
        var authContext = GetAuthContext();
        if (authContext.TenantId != EryphConstants.DefaultTenantId)
            return Task.FromResult(false);

        if (authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole))
            return Task.FromResult(true);

        // All members of the default tenant have read access but only
        // super admins have write or admin access.
        return Task.FromResult(requiredAccess == AccessRight.Read);
    }

    public IEnumerable<Guid> GetResourceRoles<TResource>(AccessRight accessRight) where TResource : Resource
    {
        var resourceType = typeof(TResource) switch
        {
            var type when type == typeof(Catlet) => ResourceType.Catlet,
            var type when type == typeof(VirtualDisk) => ResourceType.VirtualDisk,
            var type when type == typeof(VirtualNetwork) => ResourceType.VirtualNetwork,
            _ => throw new ArgumentOutOfRangeException(nameof(TResource), typeof(TResource), null)
        };
        
        return GetResourceRoles(resourceType, accessRight);
    }

    public IEnumerable<Guid> GetProjectRoles(AccessRight accessRight)
    {
        // Roles are currently hardcoded
        // The build in roles are:
        //  - owner
        //  - contributor
        //  - reader

        // The access rights included in each role are:
        //  - owner: Read, Write, Admin
        //  - contributor: Read, Write
        //  - reader: Read
        return accessRight switch
        {
            AccessRight.None => Array.Empty<Guid>(),
            AccessRight.Read => new[]
            {
                EryphConstants.BuildInRoles.Reader, EryphConstants.BuildInRoles.Contributor,
                EryphConstants.BuildInRoles.Owner
            },
            AccessRight.Write => new[]
            {
                EryphConstants.BuildInRoles.Contributor, 
                EryphConstants.BuildInRoles.Owner
            },
            AccessRight.Admin => new[]
            {
                EryphConstants.BuildInRoles.Owner
            },
            _ => throw new ArgumentOutOfRangeException(nameof(accessRight), accessRight, null)
        };
    }

    public IEnumerable<Guid> GetResourceRoles(ResourceType resourceType, 
        AccessRight accessRight)
    {
        // Resource type specific roles are currently not implemented
        // The roles are only maintained on the project level

        return GetProjectRoles(accessRight);

    }
}