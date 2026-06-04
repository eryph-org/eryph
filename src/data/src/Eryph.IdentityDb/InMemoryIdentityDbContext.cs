using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb;

/// <summary>
/// The in-memory identity store context used by tests. Mirrors <c>InMemoryStateStoreContext</c>; the
/// in-memory provider needs no provider-specific conventions, so this is just the shared model.
/// </summary>
public class InMemoryIdentityDbContext(DbContextOptions<InMemoryIdentityDbContext> options)
    : IdentityDbContext(options);
