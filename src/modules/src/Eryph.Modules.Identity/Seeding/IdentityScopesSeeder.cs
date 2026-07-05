using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using OpenIddict.Abstractions;

namespace Eryph.Modules.Identity.Seeding;

/// <summary>
/// Registers the eryph API scopes (<see cref="EryphConstants.Authorization.AllScopes"/>) as OpenIddict
/// scope resources on startup, so the token endpoint accepts them. Module-owned (not host-specific): the
/// scopes are a shared constant and both eryph-zero and the standalone identity host need them — without
/// this the token endpoint rejects every scoped request with <c>invalid_scope</c>. Add-only: an existing
/// scope is left untouched.
/// </summary>
internal class IdentityScopesSeeder(
    IOpenIddictScopeManager scopeManager)
    : IConfigSeeder<IdentityModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        foreach (var scope in EryphConstants.Authorization.AllScopes)
        {
            if (await scopeManager.FindByNameAsync(scope.Name, stoppingToken) is not null)
                continue;

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
