using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Haipa.IdentityDb
{
    public class IdentityDbContext : IdentityDbContext<ApplicationUser>
    {
        /// <summary>
        /// The Database context for aspnetcore identity and OpenIddic 
        /// </summary>
        /// <remarks>
        /// Currently no real tenant handling is implemented here, it can be added later if needed
        /// </remarks>
        /// <param name="options"></param>
        public IdentityDbContext(DbContextOptions options)
            : base(options)
        {
        }

    }
}