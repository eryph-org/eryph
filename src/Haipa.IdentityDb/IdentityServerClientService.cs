using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Haipa.IdentityDb;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.Models;


namespace Haipa.Modules.Identity.Services
{
    public class IdentityServerClientService : IIdentityServerClientService
    {
        private readonly IClientRepository _repository;

        private readonly MapperConfiguration _mapperConfiguration;
        private readonly IMapper _mapper;

        public IdentityServerClientService(IClientRepository repository)
        {
            _repository = repository;
            _mapperConfiguration = new MapperConfiguration(cfg => { cfg.AddProfile<ClientMapperProfile>(); });
            _mapper = _mapperConfiguration.CreateMapper();
        }

        public IQueryable<Client> QueryClients()
        {
            //TODO: this is a workaround for issues with automapper and projection, Mapping or queryable usage should be reconsidered
            return _repository.QueryClients().ToList().Select(x => x.ToModel()).AsQueryable();
        }

        public async Task<Client> GetClient(string clientId)
        {
            var (id, _) = await _repository.GetClientIdAsync(clientId);
            return id.GetValueOrDefault(0) == 0 ? null : (await _repository.GetClientAsync(id.Value)).ToModel();
        }

        public async Task AddClient(Client client)
        {
            await _repository.AddClientAsync(client.ToEntity());
            await _repository.SaveAllChangesAsync();
        }

        public async Task AddClients(IEnumerable<Client> clients)
        {
            await _repository.AddClientsAsync(clients.Select(x => x.ToEntity()));
            await _repository.SaveAllChangesAsync();
        }

        public async Task DeleteClient(Client client)
        {
            var trackedClient = await _repository.GetTrackedClientAsync(client.ClientId);

            if (trackedClient == null)
                return;

            await _repository.RemoveClientAsync(trackedClient);
            await _repository.SaveAllChangesAsync();
        }


        public async Task UpdateClient(Client client)
        {
            var trackedClient = await _repository.GetTrackedClientAsync(client.ClientId);

            if (trackedClient == null)
                return;

            var newEntityData = client.ToEntity();
            trackedClient = _mapper.Map(newEntityData, trackedClient);

            await _repository.UpdateClientAsync(trackedClient);
            await _repository.SaveAllChangesAsync();
        }

    }

}
