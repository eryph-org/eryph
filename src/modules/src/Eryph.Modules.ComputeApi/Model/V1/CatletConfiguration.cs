using System.Text.Json;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletConfiguration
{
    public required JsonElement Configuration { get; set; }

    public required JsonElement DeployedConfig { get; set; }

    public required string ConfigYaml { get; set; }
}
