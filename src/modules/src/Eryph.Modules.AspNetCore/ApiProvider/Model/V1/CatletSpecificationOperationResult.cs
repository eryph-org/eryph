using System.Collections.Generic;
using System.Text.Json;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class CatletSpecificationOperationResult : OperationResult
{
    public required JsonElement Configuration { get; set; }

    public required IReadOnlyDictionary<string, string> Genes { get; set; }
}
