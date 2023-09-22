using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.IdentityDb.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb
{
    public class ClientRepository<TDbContext> : IClientRepository where TDbContext : IdentityDbContext
    {
        protected readonly TDbContext DbContext;

        public ClientRepository(TDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public Task<ClientApplicationEntity> GetClientAsync(string id)
        {
            return QueryClients()
                .Where(x => x.Id == id)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }

        public Task<ClientApplicationEntity> GetTrackedClientAsync(string id)
        {
            return QueryClients()
                .Where(x => x.ClientId == id)
                .SingleOrDefaultAsync();
        }

        public IQueryable<ClientApplicationEntity> QueryClients()
        {
            return DbContext.Applications
                .Where(x=>x.IdentityApplicationType == IdentityApplicationType.Client)
                .Cast<ClientApplicationEntity>()
                .AsNoTracking();
        }



        public async Task<int> SaveAllChangesAsync()
        {
            return await DbContext.SaveChangesAsync();
        }

        public async Task<string> AddClientAsync(ClientApplicationEntity client)
        {
            await DbContext.AddAsync(client);
            return client.Id;
        }

        public Task AddClientsAsync(IEnumerable<ClientApplicationEntity> clients)
        {
            return DbContext.AddRangeAsync(clients);
        }

        public void RemoveClientRelations(ClientApplicationEntity client)
        {
        }

        public Task UpdateClientAsync(ClientApplicationEntity client)
        {
            DbContext.Update(client);
            return Task.CompletedTask;
        }

        public Task RemoveClientAsync(ClientApplicationEntity client)
        {
            DbContext.Remove(client);
            return Task.CompletedTask;
        }
    }
}