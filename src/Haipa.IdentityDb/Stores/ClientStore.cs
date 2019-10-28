using Haipa.IdentityDb.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haipa.IdentityDb.Stores
{
    public class ClientStore : IClientStore
    {
        private readonly ConfigurationStoreContext _context;
        private readonly ILogger _logger;

        public ClientStore(ConfigurationStoreContext context, ILoggerFactory loggerFactory)
        {
            _context = context;
            _logger = loggerFactory.CreateLogger("ClientStore");
        }

        public Task<Client> FindClientByIdAsync(string clientId)
        {
                var client = _context.Clients.Where(t => t.ClientId.ToString() == clientId);
                if (client.Count() == 1)
                {
                    client.First().MapDataFromEntity();
                    return Task.FromResult(client.First().Client);
                }
                else
                {
                    return null;
                }            
        }
    }
}
