using System;
using Eryph.ConfigModel.Yaml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Eryph.Core;

/// <summary>
/// YAML serializer for <see cref="EnvironmentsConfig"/>, matching eryph's config convention
/// (underscored naming, the same as the storage and network-provider config). Strict: unknown
/// members are rejected so a typo cannot be silently dropped.
/// </summary>
public static class EnvironmentsConfigYamlSerializer
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

    public static EnvironmentsConfig Deserialize(string yaml)
    {
        try
        {
            return Deserializer.Value.Deserialize<EnvironmentsConfig?>(yaml) ?? new EnvironmentsConfig();
        }
        catch (Exception ex)
        {
            throw InvalidConfigExceptionFactory.Create(ex);
        }
    }

    public static string Serialize(EnvironmentsConfig config) =>
        Serializer.Value.Serialize(config);
}
