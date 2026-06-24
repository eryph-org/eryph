using Ardalis.Specification.EntityFrameworkCore;
using JetBrains.Annotations;

namespace Eryph.IdentityDb;

public class IdentityDbRepository<T>([NotNull] IdentityDbContext dbContext)
    : RepositoryBase<T>(dbContext), IIdentityDbRepository<T>
    where T : class;
