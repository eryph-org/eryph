using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "result_type")]
[JsonDerivedType(typeof(CatletConfigOperationResult), "CatletConfig")]
[JsonDerivedType(typeof(CatletSpecificationOperationResult), "CatletSpecification")]
public abstract class OperationResult
{
}
