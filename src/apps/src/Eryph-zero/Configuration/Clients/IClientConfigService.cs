using System.Threading.Tasks;
using Eryph.Configuration.Model;

namespace Eryph.Runtime.Zero.Configuration.Clients;

internal interface IClientConfigService
{
    void Delete(string id);

    Task<ClientConfigModel?> Get(string id);

    Task Save(ClientConfigModel client);
}
