using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "result_type")]
[JsonDerivedType(typeof(CatletConfigOperationResult), "catlet_config")]
public abstract class OperationResult
{
}

public class CatletConfigOperationResult : OperationResult
{
    public JsonElement Configuration { get; set; }
}
