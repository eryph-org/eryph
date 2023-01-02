using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.StateDb.Model;

namespace Eryph.Runtime.Zero.Configuration.Storage
{
    internal class VhdWriterService : IConfigWriterService<VirtualDisk>
    {
        private readonly ConfigIO _io;

        public VhdWriterService()
        {
            _io = new ConfigIO(ZeroConfig.GetStorageConfigPath());
        }

        public async Task Delete(VirtualDisk disk, string projectName)
        {
            var storageConfig = GetStorageConfigByDiskTemplate(disk, projectName);
            storageConfig.VirtualDisks = storageConfig.VirtualDisks.Where(x => x.Id != disk.Id).ToArray();

            if (storageConfig.VirtualDisks.Length == 0)
                _io.DeleteConfigFile(storageConfig.Id);
            else
                await _io.SaveConfigFile(storageConfig, storageConfig.Id);
        }

        public Task Update(VirtualDisk disk, string projectName)
        {
            var storageConfig = GetStorageConfigByDiskTemplate(disk, projectName);
            storageConfig.VirtualDisks = storageConfig.VirtualDisks.Where(x => x.Id != disk.Id)
                .Append(new[] {new StorageVhdConfig {Id = disk.Id, Name = disk.Name, LastSeen = DateTime.UtcNow}}).ToArray();

            return _io.SaveConfigFile(storageConfig, storageConfig.Id);
        }

        public Task Add(VirtualDisk disk, string projectName)
        {
            return Update(disk, projectName);
        }

        private StorageConfig GetStorageConfigByDiskTemplate(VirtualDisk virtualDisk, string projectName)
        {
            var nameBuilder = new StringBuilder();
            nameBuilder.Append(virtualDisk.Environment ?? "$$empty$$");
            nameBuilder.Append(virtualDisk.DataStore ?? "$$empty$$");
            nameBuilder.Append(projectName ?? "$$empty$$");
            nameBuilder.Append(virtualDisk.StorageIdentifier ?? "$$empty$$");
            var hashedName = Hash(nameBuilder.ToString()).ToLowerInvariant();

            var configFile = Path.Combine(ZeroConfig.GetStorageConfigPath(), $"{hashedName}.json");

            if (!File.Exists(configFile))
                return new StorageConfig
                {
                    Id = hashedName,
                    Environment = virtualDisk.Environment,
                    DataStore = virtualDisk.DataStore,
                    Project = projectName,
                    StorageIdentifier = virtualDisk.StorageIdentifier,
                    VirtualDisks = System.Array.Empty<StorageVhdConfig>()
                };

            var configContent = File.ReadAllText(configFile);
            return JsonSerializer.Deserialize<StorageConfig>(configContent);

        }


        private static string Hash(string input)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(hash.Length * 2);

            foreach (var b in hash)
            {
                // can be "x2" if you want lowercase
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }
    }
}