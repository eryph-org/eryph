using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.StateDb.Model;
using LanguageExt;
using OpenIddict.Abstractions;

namespace Eryph.Modules.Identity.Services;

public abstract class BaseApplicationService<TEntity, TDescriptor>
    where TEntity : ApplicationEntity, new()
    where TDescriptor : ApplicationDescriptor, new()
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IIdentityDbRepository<TEntity> _repository;

    protected BaseApplicationService(IOpenIddictApplicationManager applicationManager, IIdentityDbRepository<TEntity> repository)
    {
        _applicationManager = applicationManager;
        _repository = repository;
    }

    public async ValueTask<IEnumerable<TDescriptor>> List(Guid tenantId, CancellationToken cancellationToken)
    {
        return await _repository.ListAsync(GetListSpec(tenantId), cancellationToken)
            .MapAsync(list => list.Map(async entity =>
            {
                var descriptor = new TDescriptor();
                await PopulateDescriptorFromApplication(descriptor, entity, cancellationToken);
                return descriptor;
            }).TraverseParallel(t=>t));

    }

    public async ValueTask<TDescriptor> Get(string clientId, Guid tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetBySpecAsync(GetSingleEntitySpec(clientId, tenantId), cancellationToken);
        if(entity == null)
            return default;

        var descriptor = new TDescriptor();
        await PopulateDescriptorFromApplication(descriptor, entity, cancellationToken);
        return descriptor;
    }

    public async ValueTask<TDescriptor> Update(TDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.ClientId == "system-client")
            throw new Exception("System client can't be updated");

        var currentApplication = await _repository.GetBySpecAsync(
            GetSingleEntitySpec(descriptor.ClientId, descriptor.TenantId), cancellationToken) 
                                 ?? throw new Exception("Application not found");

        var newDescriptor = descriptor.Clone<TDescriptor>();
        InitializeDescriptor(newDescriptor);


        await PopulateApplicationFromDescriptor(currentApplication, newDescriptor, cancellationToken);
        var clientSecret = descriptor.ClientSecret;
        newDescriptor.ClientSecret = null;

        if (clientSecret!= null)
            await _applicationManager.UpdateAsync(currentApplication, clientSecret, cancellationToken);
        else
            await _applicationManager.UpdateAsync(currentApplication, cancellationToken);

        var updatedApp = await _repository.GetBySpecAsync(
                             GetSingleEntitySpec(descriptor.ClientId, descriptor.TenantId), cancellationToken)
                         ?? throw new Exception("Application not found");
        newDescriptor.ClientSecret = updatedApp?.ClientSecret;
        return newDescriptor;
    }

    public async ValueTask Delete(string clientId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (clientId == "system-client")
            throw new Exception("System client can't be deleted");

        var entity = await _repository.GetBySpecAsync(GetSingleEntitySpec(clientId, tenantId), cancellationToken);
        if (entity == null)
            return;

        await _applicationManager.DeleteAsync(entity, cancellationToken);
    }

    public async ValueTask<TDescriptor> Add(TDescriptor descriptor, bool hashedSecret, CancellationToken cancellationToken)
    {
        var application = new TEntity();

        var newDescriptor = descriptor.Clone<TDescriptor>();
        InitializeDescriptor(newDescriptor);

        var clientSecret = hashedSecret ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)) : newDescriptor.ClientSecret;
        newDescriptor.ClientSecret = null;

        await PopulateApplicationFromDescriptor(application, newDescriptor, cancellationToken);
        application.Id = $"{newDescriptor.TenantId}_{newDescriptor.ClientId}";
        await _applicationManager.CreateAsync(application, clientSecret, cancellationToken);

        if (newDescriptor.ClientSecret != null && hashedSecret)
        {
            var entity =
                await _repository.GetBySpecAsync(GetSingleEntitySpec(newDescriptor.ClientId, newDescriptor.TenantId),
                    cancellationToken);
            if(entity != null)
                entity.ClientSecret = newDescriptor.ClientSecret;
        }

        return newDescriptor;
    }


    protected virtual async ValueTask PopulateDescriptorFromApplication(TDescriptor descriptor, TEntity application,
        CancellationToken cancellationToken)
    {
        await _applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.TenantId = application.TenantId;
        descriptor.AppRoles.UnionWith(application.AppRoles.Split(',', 
            StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse));
            
        var permissions = await _applicationManager.GetPermissionsAsync(application, cancellationToken);
        descriptor.Scopes.UnionWith(permissions.Where(x => x.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope))
            .Select(x => x[OpenIddictConstants.Permissions.Prefixes.Scope.Length..]));
        descriptor.ClientSecret = null;

    }


    protected virtual async ValueTask PopulateApplicationFromDescriptor(TEntity application, TDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        descriptor.Permissions.RemoveWhere(x => x.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope));

        foreach (var scope in descriptor.Scopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        await _applicationManager.PopulateAsync(application, descriptor, cancellationToken);
        application.TenantId = descriptor.TenantId;
        application.AppRoles = string.Join(',', descriptor.AppRoles.Select(g => g.ToString()));
           

    }

    protected virtual void InitializeDescriptor(TDescriptor descriptor)
    {

    }

    protected abstract ISingleResultSpecification<TEntity> GetSingleEntitySpec(string clientId, Guid tenantId);

    protected abstract ISpecification<TEntity> GetListSpec(Guid tenantId);

}