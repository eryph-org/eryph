using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Eryph.Runtime.Zero.Configuration.AgentSettings
{
    internal class VmHostAgentConfigurationManager : IVmHostAgentConfigurationManager
    {
        public EitherAsync<Error, string> GetCurrentConfigurationYaml()
        {
            return Prelude.TryAsync(async () =>
            {

                var path = ZeroConfig.GetVmHostAgentConfigPath();
                Config.EnsurePath(path);

                var configFilePath = Path.Combine(path, "agentsettings.yml");

                if (!File.Exists(configFilePath))
                {
                    await File.WriteAllTextAsync(configFilePath, VmHostAgentConfiguration.DefaultConfig);
                }

                return await File.ReadAllTextAsync(configFilePath);


            }).ToEither();
        }

        public EitherAsync<Error, VmHostAgentConfiguration> GetCurrentConfiguration()
        {
            return from yaml in GetCurrentConfigurationYaml()
                from config in ParseConfigurationYaml(yaml).ToAsync()
                select config;
        }

        public Either<Error, VmHostAgentConfiguration> ParseConfigurationYaml(string yaml)
        {
            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return Prelude.Try(() =>
                {
                    try
                    {
                        return yamlDeserializer.Deserialize<VmHostAgentConfiguration>(yaml);
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException == null)
                            throw;

                        throw ex.InnerException;
                    }
                })
                .ToEither(Error.New);
        }

        public EitherAsync<Error, Unit> SaveConfigurationYaml(string config)
        {
            return Prelude.TryAsync(async () =>
                {
                    var path = ZeroConfig.GetVmHostAgentConfigPath();
                    Config.EnsurePath(path);

                    var configFilePath = Path.Combine(path, "agentsettings.yml");

                    await File.WriteAllTextAsync(configFilePath, config);
                    return Unit.Default;
                }
            ).ToEither();
        }
    }
}
