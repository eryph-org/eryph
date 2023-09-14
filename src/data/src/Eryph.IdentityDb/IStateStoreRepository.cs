using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification.EntityFrameworkCore;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb
{
    public class IdentityDbRepository<T> : RepositoryBase<T>, IIdentityDbRepository<T>
        where T : class
    {
        public IdentityDbRepository([NotNull] IdentityDbContext dbContext) : base(dbContext)
        {
        }

    }
}