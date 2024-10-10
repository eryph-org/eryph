using System.Text.Json;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualNetworkConfiguration
{
    public required JsonElement Configuration { get; set; }
}
