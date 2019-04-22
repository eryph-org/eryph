
using Microsoft.EntityFrameworkCore;

namespace Haipa.IdentityDb
{
    public class IdentityDbContext : DbContext
    {
        public IdentityDbContext(DbContextOptions options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder) { }
    }
}
