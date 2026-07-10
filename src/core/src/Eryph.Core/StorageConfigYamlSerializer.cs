using System;
using Eryph.ConfigModel.Yaml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Eryph.Core;

/// <summary>
/// YAML serializer for <see cref="StorageConfig"/>, matching eryph's config convention
/// (underscored naming, the same as the controller settings and network-provider config). Strict:
/// unknown members are rejected so a typo cannot be silently dropped.
/// </summary>
public static class StorageConfigYamlSerializer
{
    private static readonly Lazy<IDeserializer> Deserializer = new(() =>
        new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
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

    public static StorageConfig Deserialize(string yaml)
    {
        try
        {
            return Deserializer.Value.Deserialize<StorageConfig?>(yaml) ?? new StorageConfig();
        }
        catch (Exception ex)
        {
            throw InvalidConfigExceptionFactory.Create(ex);
        }
    }

    public static string Serialize(StorageConfig config) =>
        Serializer.Value.Serialize(config);
}
