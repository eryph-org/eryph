using System;
using System.Threading.Tasks;
using Eryph.Configuration.Model;

namespace Eryph.Runtime.Zero.Configuration.Clients;

internal class ClientConfigService : IClientConfigService
{
    private readonly ConfigIO _configIO = new(ZeroConfig.GetClientConfigPath());

    public void Delete(string id)
    {
        _configIO.DeleteConfigFile(id);
    }

    public Task<ClientConfigModel?> Get(string id)
    {
        return _configIO.ReadConfigFile<ClientConfigModel>(id);
    }

    public Task Save(ClientConfigModel client)
    {
        return _configIO.SaveConfigFile(client, client.ClientId ?? throw new InvalidOperationException("ClientId is required to save a client config."));
    }
}
