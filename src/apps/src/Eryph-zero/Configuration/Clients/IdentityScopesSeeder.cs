using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.Modules.Identity;
using JetBrains.Annotations;
using OpenIddict.Abstractions;

namespace Eryph.Runtime.Zero.Configuration.Clients;

[UsedImplicitly]
internal class IdentityScopesSeeder(
    IOpenIddictScopeManager scopeManager)
    : IConfigSeeder<IdentityModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        foreach (var scope in EryphConstants.Authorization.AllScopes)
        {
            if (await scopeManager.FindByNameAsync(scope.Name, stoppingToken) is not null) continue;

            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = scope.Name,
                Description = scope.Description,
            };
                    
            descriptor.Resources.UnionWith(scope.Resources);

            await scopeManager.CreateAsync(descriptor, stoppingToken);
        }
    }
}
