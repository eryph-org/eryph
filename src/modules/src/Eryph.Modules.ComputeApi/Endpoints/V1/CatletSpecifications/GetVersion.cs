using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using AutoMapper;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

[Route("v{version:apiVersion}")]
public class GetVersion(
    IMapper mapper,
    IReadonlyStateStoreRepository<StateDb.Model.CatletSpecification> specificationRepository,
    IReadonlyStateStoreRepository<StateDb.Model.CatletSpecificationVersion> specificationVersionRepository,
    IUserRightsProvider userRightsProvider)
    : EndpointBaseAsync
        .WithRequest<GetCatletSpecificationVersionRequest>
        .WithActionResult<CatletSpecificationVersion>
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications/{specification_id}/versions/{id}")]
    [SwaggerOperation(
        Summary = "Get a catlet specification version",
        Description = "Get a catlet specification version",
        OperationId = "CatletSpecifications_GetVersion",
        Tags = ["Catlet Specifications"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(CatletSpecificationVersion),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<CatletSpecificationVersion>> HandleAsync(
        [FromRoute] GetCatletSpecificationVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.SpecificationId, out var specificationId))
            return new NotFoundResult();

        if (!Guid.TryParse(request.Id, out var id))
            return new NotFoundResult();

        var specificationExists = await specificationRepository.AnyAsync(
            new ResourceSpecs<StateDb.Model.CatletSpecification>.GetById(
                specificationId,
                userRightsProvider.GetAuthContext(),
                userRightsProvider.GetResourceRoles<StateDb.Model.CatletSpecification>(StateDb.Model.AccessRight.Read)),
            cancellationToken);

        if (!specificationExists)
            return new NotFoundResult();

        var dbSpecificationVersion = await specificationVersionRepository.GetBySpecAsync(
            new CatletSpecificationVersionSpecs.GetByIdReadOnly(specificationId, id),
            cancellationToken);

        var mappedResult = mapper.Map<CatletSpecificationVersion>(
            dbSpecificationVersion,
            o => o.SetAuthContext(userRightsProvider.GetAuthContext()));

        return new JsonResult(mappedResult);
    }
}
