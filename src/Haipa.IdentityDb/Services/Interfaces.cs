using Haipa.IdentityDb.Dtos;
using Haipa.IdentityDb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haipa.IdentityDb.Services.Interfaces
{
    public interface IClientEntityService
    {
        IQueryable<ClientEntityDTO> GetClient();
        Task<int> PostClient(ClientEntityDTO client);
        Task<int> PutClient(ClientEntityDTO client);
        Task<int> DeleteClient(Guid clientId);
    }
}
