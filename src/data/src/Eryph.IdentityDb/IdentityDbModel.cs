using Eryph.IdentityDb.Entities;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace Eryph.IdentityDb;

/// <summary>
/// Applies the OpenIddict Entity Framework Core model to the <see cref="IdentityDbContext"/> options.
/// OpenIddict contributes its tables (applications, authorizations, scopes, tokens) through this
/// options extension — not through the context's <c>OnModelCreating</c>. Both the runtime registration
/// (<c>IdentityModule.ConfigureServices</c>) and the design-time factory that generates migrations must
/// apply it; otherwise a generated migration silently omits every OpenIddict table. Keeping the single
/// call here is the one source of truth shared by both.
/// </summary>
public static class IdentityDbModel
{
    public static void ApplyOpenIddict(DbContextOptionsBuilder options) =>
        options.UseOpenIddict<ApplicationEntity, AuthorizationEntity,
            OpenIddictEntityFrameworkCoreScope, TokenEntity, string>();
}
