using System.Threading.Tasks;
using Haipa.Configuration;
using Haipa.Configuration.Model;

namespace Haipa.Runtime.Zero.Configuration.Clients
{
    internal class ClientConfigWriterService : IConfigWriterService<ClientConfigModel>
    {
        private readonly ConfigIO _io;

        public ClientConfigWriterService()
        {
            _io = new ConfigIO(ZeroConfig.GetClientConfigPath());
        }

        public Task Delete(ClientConfigModel client)
        {
            _io.DeleteConfigFile(client.ClientId);
            return Task.CompletedTask;
        }

        public Task Update(ClientConfigModel client)
        {
            return _io.SaveConfigFile(client, client.ClientId);
        }

        public Task Add(ClientConfigModel client)
        {
            return _io.SaveConfigFile(client, client.ClientId);
        }
    }
}