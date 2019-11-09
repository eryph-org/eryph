using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haipa.IdentityDb.Models;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Haipa.IdentityDb
{
    public class ConfigurationStoreContext : DbContext
    {
      
        public ConfigurationStoreContext(DbContextOptions<ConfigurationStoreContext> options) : base(options)
        { }

        public DbSet<ClientEntity> Clients { get; set; }
        public DbSet<ApiResourceEntity> ApiResources { get; set; }
        public DbSet<IdentityResourceEntity> IdentityResources { get; set; }
        
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<ClientEntity>().HasKey(m => m.ClientId);
            builder.Entity<ApiResourceEntity>().HasKey(m => m.ApiResourceName);
            builder.Entity<IdentityResourceEntity>().HasKey(m => m.IdentityResourceName);
            base.OnModelCreating(builder);
        }
    }
    
}
