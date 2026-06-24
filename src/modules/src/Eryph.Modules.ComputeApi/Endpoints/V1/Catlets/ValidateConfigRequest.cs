using System.Text.Json;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class ValidateConfigRequest
{
    public required JsonElement Configuration { get; set; }
}
