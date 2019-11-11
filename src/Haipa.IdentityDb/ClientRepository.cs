using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace Haipa.IdentityDb
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
                .Select(x => new { x.Id, x.ClientId })
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