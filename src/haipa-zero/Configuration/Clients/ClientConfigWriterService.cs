using System.Threading.Tasks;
using Haipa.Configuration;
using Haipa.Configuration.Model;

namespace Haipa.Runtime.Zero.Configuration.Clients
{
    internal class ClientConfigWriterService : IConfigWriterService<ClientConfigModel>
    {
        public Task Delete(ClientConfigModel client)
        {
            client.DeleteConfigFile();
            return Task.CompletedTask;
        }

        public Task Update(ClientConfigModel client)
        {
            return client.SaveConfigFile();

        }

        public Task Add(ClientConfigModel client)
        {
            return client.SaveConfigFile();
        }
    }
}