using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Haipa.Modules.Identity.IdentityServer
{
    //public class ConfigurationStoreContext : DbContext
    //{
    //    //6
    //    public class ApiResourceEntity
    //    {
    //        public string ApiResourceData { get; set; }

    //        [Key]
    //        public string ApiResourceName { get; set; }

    //        [NotMapped]
    //        public ApiResource ApiResource { get; set; }

    //        public void AddDataToEntity()
    //        {
    //            ApiResourceData = JsonConvert.SerializeObject(ApiResource);
    //            ApiResourceName = ApiResource.Name;
    //        }

    //        public void MapDataFromEntity()
    //        {
    //            ApiResource = JsonConvert.DeserializeObject<ApiResource>(ApiResourceData);
    //            ApiResourceName = ApiResource.Name;
    //        }
    //    }

    //    //5
    //    public class IdentityResourceEntity
    //    {
    //        public string IdentityResourceData { get; set; }

    //        [Key]
    //        public string IdentityResourceName { get; set; }

    //        [NotMapped]
    //        public IdentityResource IdentityResource { get; set; }

    //        public void AddDataToEntity()
    //        {
    //            IdentityResourceData = JsonConvert.SerializeObject(IdentityResource);
    //            IdentityResourceName = IdentityResource.Name;
    //        }

    //        public void MapDataFromEntity()
    //        {
    //            IdentityResource = JsonConvert.DeserializeObject<IdentityResource>(IdentityResourceData);
    //            IdentityResourceName = IdentityResource.Name;
    //        }
    //    }

    //    //4
    //    public class ResourceStore : IResourceStore
    //    {
    //        private readonly ConfigurationStoreContext _context;
    //        private readonly ILogger _logger;

    //        public ResourceStore(ConfigurationStoreContext context, ILoggerFactory loggerFactory)
    //        {
    //            _context = context;
    //            _logger = loggerFactory.CreateLogger("ResourceStore");
    //        }

    //        public Task<ApiResource> FindApiResourceAsync(string name)
    //        {
    //            var apiResource = _context.ApiResources.First(t => t.ApiResourceName == name);
    //            apiResource.MapDataFromEntity();
    //            return Task.FromResult(apiResource.ApiResource);
    //        }

    //        public Task<IEnumerable<ApiResource>> FindApiResourcesByScopeAsync(IEnumerable<string> scopeNames)
    //        {
    //            if (scopeNames == null) throw new ArgumentNullException(nameof(scopeNames));


    //            var apiResources = new List<ApiResource>();
    //            var apiResourcesEntities = from i in _context.ApiResources
    //                                       where scopeNames.Contains(i.ApiResourceName)
    //                                       select i;

    //            foreach (var apiResourceEntity in apiResourcesEntities)
    //            {
    //                apiResourceEntity.MapDataFromEntity();

    //                apiResources.Add(apiResourceEntity.ApiResource);
    //            }

    //            return Task.FromResult(apiResources.AsEnumerable());
    //        }

    //        public Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeAsync(IEnumerable<string> scopeNames)
    //        {
    //            if (scopeNames == null) throw new ArgumentNullException(nameof(scopeNames));

    //            var identityResources = new List<IdentityResource>();
    //            var identityResourcesEntities = from i in _context.IdentityResources
    //                                            where scopeNames.Contains(i.IdentityResourceName)
    //                                            select i;

    //            foreach (var identityResourceEntity in identityResourcesEntities)
    //            {
    //                identityResourceEntity.MapDataFromEntity();

    //                identityResources.Add(identityResourceEntity.IdentityResource);
    //            }

    //            return Task.FromResult(identityResources.AsEnumerable());
    //        }

    //        //public Task<Resources> GetAllResourcesAsync()
    //        //{
    //        //    var apiResourcesEntities = _context.ApiResources.ToList();
    //        //    var identityResourcesEntities = _context.IdentityResources.ToList();

    //        //    var apiResources = new List<ApiResource>();
    //        //    var identityResources = new List<IdentityResource>();

    //        //    foreach (var apiResourceEntity in apiResourcesEntities)
    //        //    {
    //        //        apiResourceEntity.MapDataFromEntity();

    //        //        apiResources.Add(apiResourceEntity.ApiResource);
    //        //    }

    //        //    foreach (var identityResourceEntity in identityResourcesEntities)
    //        //    {
    //        //        identityResourceEntity.MapDataFromEntity();

    //        //        identityResources.Add(identityResourceEntity.IdentityResource);
    //        //    }

    //        //    var result = new Resources(identityResources, apiResources);
    //        //    return Task.FromResult(result);
    //        //}

    //        Task<IdentityServer4.Models.Resources> IResourceStore.GetAllResourcesAsync()
    //        {
    //            var apiResourcesEntities = _context.ApiResources.ToList();
    //            var identityResourcesEntities = _context.IdentityResources.ToList();

    //            var apiResources = new List<ApiResource>();
    //            var identityResources = new List<IdentityResource>();

    //            foreach (var apiResourceEntity in apiResourcesEntities)
    //            {
    //                apiResourceEntity.MapDataFromEntity();

    //                apiResources.Add(apiResourceEntity.ApiResource);
    //            }

    //            foreach (var identityResourceEntity in identityResourcesEntities)
    //            {
    //                identityResourceEntity.MapDataFromEntity();

    //                identityResources.Add(identityResourceEntity.IdentityResource);
    //            }

    //            IdentityServer4.Models.Resources result = new IdentityServer4.Models.Resources (identityResources, apiResources);
    //            return Task.FromResult(result);
    //            // throw new NotImplementedException();
    //        }
    //    }

    //    //3
    //    public ConfigurationStoreContext(DbContextOptions<ConfigurationStoreContext> options) : base(options)
    //    { }

    //    public DbSet<ClientEntity> Clients { get; set; }
    //    public DbSet<ApiResourceEntity> ApiResources { get; set; }
    //    public DbSet<IdentityResourceEntity> IdentityResources { get; set; }


    //    protected override void OnModelCreating(ModelBuilder builder)
    //    {
    //        builder.Entity<ClientEntity>().HasKey(m => m.ClientId);
    //        builder.Entity<ApiResourceEntity>().HasKey(m => m.ApiResourceName);
    //        builder.Entity<IdentityResourceEntity>().HasKey(m => m.IdentityResourceName);
    //        base.OnModelCreating(builder);
    //    }
    //}

    ////2
    //public class ClientEntity
    //{
    //    public string ClientData { get; set; }

    //    [Key]
    //    public string ClientId { get; set; }

    //    [NotMapped]
    //    public Client Client { get; set; }

    //    public void AddDataToEntity()
    //    {
    //        ClientData = JsonConvert.SerializeObject(Client);
    //        ClientId = Client.ClientId;
    //    }

    //    public void MapDataFromEntity()
    //    {
    //        Client = JsonConvert.DeserializeObject<Client>(ClientData);
    //        ClientId = Client.ClientId;
    //    }
    //}

    ////1
    //public class ClientStore : IClientStore
    //{
    //    private readonly ConfigurationStoreContext _context;
    //    private readonly ILogger _logger;

    //    public ClientStore(ConfigurationStoreContext context, ILoggerFactory loggerFactory)
    //    {
    //        _context = context;
    //        _logger = loggerFactory.CreateLogger("ClientStore");
    //    }

    //    public Task<Client> FindClientByIdAsync(string clientId)
    //    {
    //        var client = _context.Clients.First(t => t.ClientId == clientId);
    //        client.MapDataFromEntity();
    //        return Task.FromResult(client.Client);
    //    }
    //}
}
