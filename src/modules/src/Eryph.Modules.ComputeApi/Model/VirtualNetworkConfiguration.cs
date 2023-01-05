using System.Text.Json;

namespace Eryph.Modules.ComputeApi.Model;

public class VirtualNetworkConfiguration
{
    public VirtualNetworkConfiguration()
    {
    }

    public JsonElement Configuration { get; set; }


}