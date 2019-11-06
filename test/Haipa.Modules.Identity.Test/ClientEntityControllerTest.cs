using Haipa.IdentityDb.Dtos;
using Haipa.IdentityDb.Services.Interfaces;
using Haipa.Modules.Identity.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Haipa.Modules.Identity.Test
{
    public class ClientEntityControllerTest
    {
        //{
        //    [Fact]
        //    public GetResolvedClients()
        //    {

        //    }
        public class ServiceMock : IClientEntityService
        {
            public Task<int> DeleteClient(Guid clientId)
            {
                throw new NotImplementedException();
            }

            public IQueryable<ClientEntityDTO> GetClient()
            {
                List<ClientEntityDTO> o = new List<ClientEntityDTO>();
                o.Add(new ClientEntityDTO { ClientId = Guid.NewGuid() });
                return o.AsEnumerable().AsQueryable();
            }

            public Task<int> PostClient(ClientEntityDTO client)
            {
                throw new NotImplementedException();
            }

            public Task<int> PutClient(ClientEntityDTO client)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public async Task GetResolvedClients()
        {
            ServiceMock o = new ServiceMock();
            ClientEntityController a = new ClientEntityController(o);
            Assert.NotEmpty(a.Get());

        }
    }
}
