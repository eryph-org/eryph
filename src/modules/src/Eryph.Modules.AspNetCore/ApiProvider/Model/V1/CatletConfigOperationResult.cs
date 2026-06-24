using System.Text.Json;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class CatletConfigOperationResult : OperationResult
{
    public required JsonElement Configuration { get; set; }
}
