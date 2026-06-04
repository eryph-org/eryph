using System.Collections.Generic;
using System.Linq;
using Eryph.IdentityDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eryph.Modules.Identity.ChangeTracking;

/// <summary>
/// Decorates the identity <see cref="IDbContextConfigurer{IdentityDbContext}"/> so the change-tracking
/// interceptors are attached to the context. The identity analog of the state store's
/// <c>ChangeTrackingDbConfigurer</c>; registered only when change tracking is enabled.
/// </summary>
internal class ChangeTrackingIdentityDbConfigurer(
    IDbContextConfigurer<IdentityDbContext> decoratedConfigurer,
    IEnumerable<IDbTransactionInterceptor> interceptors)
    : IDbContextConfigurer<IdentityDbContext>
{
    public void Configure(DbContextOptionsBuilder options)
    {
        decoratedConfigurer.Configure(options);
        options.AddInterceptors(interceptors.ToList());
    }
}
