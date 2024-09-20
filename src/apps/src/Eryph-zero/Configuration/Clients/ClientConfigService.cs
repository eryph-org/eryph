using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        return _configIO.SaveConfigFile(client, client.ClientId);
    }
}
