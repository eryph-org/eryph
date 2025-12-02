using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class DeleteCatletSpecificationRequest : SingleEntityRequest
{
    [FromBody] public required DeleteCatletSpecificationRequestBody Body { get; set; }
}

public class DeleteCatletSpecificationRequestBody
{
    public bool? DeleteCatlet { get; set; }
}
