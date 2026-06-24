using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using AutoMapper;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

[Route("v{version:apiVersion}")]
public class GetVersion(
    IMapper mapper,
    IReadonlyStateStoreRepository<CatletSpecification> specificationRepository,
    IReadonlyStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    IUserRightsProvider userRightsProvider)
    : EndpointBaseAsync.WithRequest<GetCatletSpecificationVersionRequest>.WithActionResult<
        Model.V1.CatletSpecificationVersion>
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications/{specification_id}/versions/{id}")]
    [SwaggerOperation(
            Summary = "Get a catlet specification version",
            Description = "Get a catlet specification version",
            OperationId = "CatletSpecifications_GetVersion",
            Tags = ["Catlet Specifications"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(Model.V1.CatletSpecificationVersion),
            "application/json"),
    ]
    public override async Task<ActionResult<Model.V1.CatletSpecificationVersion>> HandleAsync(
        [FromRoute] GetCatletSpecificationVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.SpecificationId, out var specificationId))
            return new NotFoundResult();

        if (!Guid.TryParse(request.Id, out var id))
            return new NotFoundResult();

        var specificationExists = await specificationRepository.AnyAsync(
            new ResourceSpecs<CatletSpecification>.GetById(
                specificationId,
                userRightsProvider.GetAuthContext(),
                userRightsProvider.GetResourceRoles<CatletSpecification>(AccessRight.Read)),
            cancellationToken);

        if (!specificationExists)
            return new NotFoundResult();

        var dbSpecificationVersion = await specificationVersionRepository.GetBySpecAsync(
            new CatletSpecificationVersionSpecs.GetByIdReadOnly(specificationId, id),
            cancellationToken);

        var mappedResult = mapper.Map<Model.V1.CatletSpecificationVersion>(
            dbSpecificationVersion,
            o => o.SetAuthContext(userRightsProvider.GetAuthContext()));

        return new JsonResult(mappedResult);
    }
}
