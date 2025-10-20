using System.Collections.Generic;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class DeployCatletSpecificationRequest : SingleEntityRequest
{
    [FromBody] public required DeployCatletSpecificationRequestBody Body { get; set; }
}

public class DeployCatletSpecificationRequestBody
{
    public required IReadOnlyDictionary<string, string> Variables { get; set; }
}
