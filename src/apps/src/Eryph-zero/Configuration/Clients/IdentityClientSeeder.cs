using System;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.Seeding;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Services;
using JetBrains.Annotations;

namespace Eryph.Runtime.Zero.Configuration.Clients;

[UsedImplicitly]
internal class IdentityClientSeeder(
    IFileSystem fileSystem,
    IClientService clientService)
    : IConfigSeeder<IdentityModule>
{
    private readonly string _configPath = ZeroConfig.GetClientConfigPath();

    public async Task Execute(CancellationToken stoppingToken)
    {
        var files = fileSystem.Directory.EnumerateFiles(_configPath, "*.json");
        foreach (var file in files)
        {
            try
            {
                var content = await fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                var clientConfig = JsonSerializer.Deserialize<ClientConfigModel>(content);
                var clientDescriptor = clientConfig.ToDescriptor();
                await clientService.Add(clientDescriptor, true, stoppingToken);
            }
            catch (Exception ex)
            {
                throw new SeederException($"Failed to seed identity client from file '{file}'", ex);
            }
        }
    }
}
