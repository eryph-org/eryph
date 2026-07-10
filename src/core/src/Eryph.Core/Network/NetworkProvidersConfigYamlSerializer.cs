using System;
using Eryph.ConfigModel.Yaml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
            // Deserialize to a nullable target — YamlDotNet returns null for a null/empty document —
            // and coalesce so the method's non-null contract actually holds (callers do not guard).
            return Deserializer.Value.Deserialize<NetworkProvidersConfiguration?>(yaml)
                   ?? new NetworkProvidersConfiguration();
        }
        catch (Exception ex)
        {
            throw InvalidConfigExceptionFactory.Create(ex);
        }
    }

    public static string Serialize(NetworkProvidersConfiguration config) =>
        Serializer.Value.Serialize(config);
}
