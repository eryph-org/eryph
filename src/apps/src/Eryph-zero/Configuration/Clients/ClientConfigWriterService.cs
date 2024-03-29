﻿using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    internal class ClientConfigWriterService : IConfigWriterService<ClientConfigModel>
    {
        private readonly ConfigIO _io = new(ZeroConfig.GetClientConfigPath());

        public Task Delete(ClientConfigModel client, string projectName)
        {
            _io.DeleteConfigFile(client.ClientId);
            return Task.CompletedTask;
        }

        public Task Update(ClientConfigModel client, string projectName)
        {
            return _io.SaveConfigFile(client, client.ClientId);
        }

        public Task Add(ClientConfigModel client, string projectName)
        {
            return _io.SaveConfigFile(client, client.ClientId);
        }
    }
}