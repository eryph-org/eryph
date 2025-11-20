using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class ListCatletSpecificationVersionsRequest : RequestBase, IListRequest
{
    [FromRoute(Name = "specification_id")] public required string SpecificationId { get; set; }
}
