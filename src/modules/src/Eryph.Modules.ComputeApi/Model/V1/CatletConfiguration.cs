using System.Text.Json;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletConfiguration
{
    public required JsonElement Configuration { get; set; }
}
