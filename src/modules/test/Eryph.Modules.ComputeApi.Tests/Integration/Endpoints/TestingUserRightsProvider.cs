using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Resources;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
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

/// <summary>
/// The default options to use to when creating
/// <see cref="HttpClient"/> instances by calling
/// <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>.
/// </summary>
public class WebModuleFactoryClientOptions
{
    /// <summary>
    /// Initializes a new instance of <see cref="WebModuleFactoryClientOptions"/>.
    /// </summary>
    public WebModuleFactoryClientOptions()
    {
    }

    // Copy constructor
    internal WebModuleFactoryClientOptions(WebModuleFactoryClientOptions clientOptions)
    {
        BaseAddress = clientOptions.BaseAddress;
        AllowAutoRedirect = clientOptions.AllowAutoRedirect;
        MaxAutomaticRedirections = clientOptions.MaxAutomaticRedirections;
        HandleCookies = clientOptions.HandleCookies;
    }

    /// <summary>
    /// Gets or sets the base address of <see cref="HttpClient"/> instances created by calling
    /// <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>.
    /// The default is <c>http://localhost</c>.
    /// </summary>
    public Uri BaseAddress { get; set; } = new Uri("http://localhost");

    /// <summary>
    /// Gets or sets whether or not <see cref="HttpClient"/> instances created by calling
    /// <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>
    /// should automatically follow redirect responses.
    /// The default is <c>true</c>.
    /// /// </summary>
    public bool AllowAutoRedirect { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of redirect responses that <see cref="HttpClient"/> instances
    /// created by calling <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>
    /// should follow.
    /// The default is <c>7</c>.
    /// </summary>
    public int MaxAutomaticRedirections { get; set; } = 7;

    /// <summary>
    /// Gets or sets whether <see cref="HttpClient"/> instances created by calling 
    /// <see cref="WebModuleFactory{TModule}.CreateClient(WebModuleFactoryClientOptions)"/>
    /// should handle cookies.
    /// The default is <c>true</c>.
    /// </summary>
    public bool HandleCookies { get; set; } = true;

    internal DelegatingHandler[] CreateHandlers()
    {
        return CreateHandlersCore().ToArray();

        IEnumerable<DelegatingHandler> CreateHandlersCore()
        {
            if (AllowAutoRedirect)
            {
                yield return new RedirectHandler(MaxAutomaticRedirections);
            }
            if (HandleCookies)
            {
                yield return new CookieContainerHandler();
            }
        }
    }
}