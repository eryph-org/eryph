using System.Text.Json;
using System.Text.Json.Nodes;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Yaml;

namespace Eryph.Packer;

public static class VMExport
{
    public static async Task<IEnumerable<PackableFile>> ExportToPackable(DirectoryInfo vmExport, string genesetFolder,
        CancellationToken token)
    {

        var result = new List<PackableFile>();
        var vmPlan = ConvertVMDataToConfig(vmExport) ?? new CatletConfig();
        var configJson = ConfigModelJsonSerializer.Serialize(vmPlan);
        var catletJsonFilePath = Path.Combine(genesetFolder, "catlet.json");
        var configYaml = CatletConfigYamlSerializer.Serialize(vmPlan);
        var catletYamlFilePath = Path.Combine(genesetFolder, "catlet.yaml");

        await File.WriteAllTextAsync(catletJsonFilePath, configJson, token);
        await File.WriteAllTextAsync(catletYamlFilePath, configYaml, token);

        result.Add(new PackableFile(catletJsonFilePath, "catlet.json", GeneType.Catlet, "catlet", false));

        var vhdFiles = vmExport.GetFiles("*.vhdx", SearchOption.AllDirectories);

        foreach (var vhdFile in vhdFiles)
        {
            token.ThrowIfCancellationRequested();

            result.Add(new PackableFile(vhdFile.FullName, vhdFile.Name,
                GeneType.Volume, Path.GetFileNameWithoutExtension(vhdFile.Name), true));
        }


        return result;

    }

    private static CatletConfig? ConvertVMDataToConfig(DirectoryInfo vmExport)
    {
        var vmConfigFile = vmExport.GetFiles("vm.json").FirstOrDefault();
        if (vmConfigFile == null)
            return null;


        try
        {
            using var vmStream = vmConfigFile.OpenRead();
            var configJson = JsonSerializer.Deserialize<JsonNode>(vmStream);
            if (configJson == null)
                return null;

            var vmJson = configJson["vm"];
            var firmwareJson = configJson["firmware"];
            var processorJson = configJson["processor"];
            var securityJson = configJson["security"];


            var dynamicMemory = (vmJson?["DynamicMemoryEnabled"]?.GetValue<bool>()).GetValueOrDefault();

            var capabilities = new List<CatletCapabilityConfig>();

            if (!string.IsNullOrWhiteSpace(firmwareJson?["SecureBootTemplate"]?.GetValue<string>()))
                capabilities.Add(new CatletCapabilityConfig
                {
                    Name = "SecureBoot",
                    Details = new[] { "Template:" + firmwareJson?["SecureBootTemplate"]?.GetValue<string>() }
                });

            if ((processorJson?["ExposeVirtualizationExtensions"]?.GetValue<bool>()).GetValueOrDefault())
            {
                capabilities.Add(new CatletCapabilityConfig
                    {
                        Name = "NestedVirtualization"
                    }
                );
            }

            if ((securityJson?["TpmEnabled"]?.GetValue<bool>()).GetValueOrDefault())
            {
                string[]? details = null;
                if ((securityJson?["EncryptStateAndVmMigrationTraffic"]?.GetValue<bool>()).GetValueOrDefault())
                    details = new[] { "TrafficEncryption" };

                capabilities.Add(new CatletCapabilityConfig
                    {
                        Name = "TPM",
                        Details = details
                    }
                );
            }

            var result = new CatletConfig
            {
                Cpu = new CatletCpuConfig
                {
                    Count = vmJson?["ProcessorCount"]?.GetValue<int>()
                },
                Memory = new CatletMemoryConfig
                {
                    Startup = (int)Math.Ceiling((vmJson?["MemoryStartup"]?.GetValue<long>()).GetValueOrDefault() /
                                                1024d / 1024),
                    Maximum = dynamicMemory
                        ? (int)Math.Ceiling((vmJson?["MemoryMaximum"]?.GetValue<long>()).GetValueOrDefault() / 1024d /
                                            1024)
                        : null,
                    Minimum = dynamicMemory
                        ? (int)Math.Ceiling((vmJson?["MemoryMinimum"]?.GetValue<long>()).GetValueOrDefault() / 1024d /
                                            1024)
                        : null,
                },
                NetworkAdapters = vmJson?["NetworkAdapters"]?.AsArray().Select(adapterNode =>
                    new CatletNetworkAdapterConfig { Name = adapterNode?["Name"]?.GetValue<string>() }).ToArray(),
                Drives = vmJson?["HardDrives"]?.AsArray().Select(driveNode => new CatletDriveConfig
                {
                    Name = Path.GetFileNameWithoutExtension(driveNode?["Path"]?.GetValue<string>())
                }).ToArray(),
                Capabilities = capabilities.ToArray(),
            };

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to convert vm.json to Catlet config", ex);
        }

    }

}