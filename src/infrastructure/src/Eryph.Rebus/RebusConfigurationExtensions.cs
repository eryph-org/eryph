using Rebus.Config;
using Rebus.Serialization;
using Rebus.Serialization.Json;

namespace Eryph.Rebus;

public static class RebusConfigurationExtensions
{
    public static void UseEryphSettings(this StandardConfigurer<ISerializer> serializer)
    {
        serializer.UseSystemTextJson(EryphJsonSerializerOptions.Options);
    }
}
