using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using Eryph.ConfigModel.Yaml;

namespace Eryph.Core.Network;

public static class NetworkProvidersConfigYamlSerializer
{
    private static readonly Lazy<IDeserializer> Deserializer = new(() =>
        new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithEnforceRequiredMembers()
            .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
            .Build());


    private static readonly Lazy<ISerializer> Serializer = new(() =>
        new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull
                                            | DefaultValuesHandling.OmitEmptyCollections)
            .DisableAliases()
            .Build());

    public static NetworkProvidersConfiguration Deserialize(string yaml)
    {
        try
        {
            return Deserializer.Value.Deserialize<NetworkProvidersConfiguration>(yaml);
        }
        catch (Exception ex)
        {
            throw InvalidConfigExceptionFactory.Create(ex);
        }
    }

    public static string Serialize(NetworkProvidersConfiguration config) =>
        Serializer.Value.Serialize(config);
}
