using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.IdentityServer.EfCore.Storage.DbContexts;
using Dbosoft.IdentityServer.EfCore.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb
{
    public class ClientRepository<TDbContext> : IClientRepository where TDbContext : ConfigurationDbContext
    {
        protected readonly TDbContext DbContext;

        public ClientRepository(TDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public Task<Client> GetClientAsync(int id)
        {
            return QueryClients()
                .Where(x => x.Id == id)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }

        public Task<Client> GetTrackedClientAsync(string id)
        {
            return QueryClients()
                .Where(x => x.ClientId == id)
                .SingleOrDefaultAsync();
        }

        public IQueryable<Client> QueryClients()
        {
            return DbContext.Clients
                .Include(x => x.AllowedGrantTypes)
                .Include(x => x.RedirectUris)
                .Include(x => x.PostLogoutRedirectUris)
                .Include(x => x.AllowedScopes)
                .Include(x => x.ClientSecrets)
                .Include(x => x.Claims)
                .Include(x => x.IdentityProviderRestrictions)
                .Include(x => x.AllowedCorsOrigins)
                .Include(x => x.Properties)
                .AsNoTracking();
        }


        public async Task<(int? Id, string ClientId)> GetClientIdAsync(string clientId)
        {
            var client = await DbContext.Clients.Where(x => x.ClientId == clientId)
                .Select(x => new {x.Id, x.ClientId})
                .SingleOrDefaultAsync();

            return (client?.Id, client?.ClientId);
        }


        public async Task<int> SaveAllChangesAsync()
        {
            return await DbContext.SaveChangesAsync();
        }

        public async Task<int> AddClientAsync(Client client)
        {
            await DbContext.AddAsync(client);
            return client.Id;
        }

        public Task AddClientsAsync(IEnumerable<Client> clients)
        {
            return DbContext.AddRangeAsync(clients);
        }

        public void RemoveClientRelations(Client client)
        {
            DbContext.RemoveRange(client.AllowedScopes);
            DbContext.RemoveRange(client.AllowedGrantTypes);
            DbContext.RemoveRange(client.RedirectUris);
            DbContext.RemoveRange(client.AllowedCorsOrigins);
            DbContext.RemoveRange(client.IdentityProviderRestrictions);
            DbContext.RemoveRange(client.PostLogoutRedirectUris);
        }

        public Task UpdateClientAsync(Client client)
        {
            DbContext.Update(client);
            return Task.CompletedTask;
        }

        public Task RemoveClientAsync(Client client)
        {
            DbContext.Remove(client);
            return Task.CompletedTask;
        }
    }
}