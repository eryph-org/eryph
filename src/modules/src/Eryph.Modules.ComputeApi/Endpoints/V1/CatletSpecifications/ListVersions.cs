using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using AutoMapper;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using CatletSpecification = Eryph.StateDb.Model.CatletSpecification;
using CatletSpecificationVersion = Eryph.StateDb.Model.CatletSpecificationVersion;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

[Route("v{version:apiVersion}")]
public class ListVersions(
    IMapper mapper,
    IReadonlyStateStoreRepository<CatletSpecification> specificationRepository,
    IReadonlyStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    IUserRightsProvider userRightsProvider)
    : EndpointBaseAsync.WithRequest<ListCatletSpecificationVersionsRequest>.WithActionResult<
        ListResponse<CatletSpecificationVersionInfo>>
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications/{specification_id}/versions")]
    [SwaggerOperation(
            Summary = "List all catlet specification versions",
            Description = "List all catlet specification versions",
            OperationId = "CatletSpecifications_ListVersions",
            Tags = ["Catlet Specifications"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(ListResponse<CatletSpecificationVersionInfo>),
            "application/json"),
    ]
    public override async Task<ActionResult<ListResponse<CatletSpecificationVersionInfo>>> HandleAsync(
        [FromRoute] ListCatletSpecificationVersionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.SpecificationId, out var specificationId))
            return new NotFoundResult();

        var specificationExists = await specificationRepository.AnyAsync(
            new ResourceSpecs<CatletSpecification>.GetById(
                specificationId,
                userRightsProvider.GetAuthContext(),
                userRightsProvider.GetResourceRoles<CatletSpecification>(AccessRight.Read)),
            cancellationToken);

        if (!specificationExists)
            return new NotFoundResult();

        var dpSpecificationVersions = await specificationVersionRepository.ListAsync(
            new CatletSpecificationVersionSpecs.ListBySpecificationIdReadOnly(specificationId),
            cancellationToken);

        var mappedResults = mapper.Map<IReadOnlyList<CatletSpecificationVersionInfo>>(
            dpSpecificationVersions,
            o => o.SetAuthContext(userRightsProvider.GetAuthContext()));

        return new JsonResult(new ListResponse<CatletSpecificationVersionInfo> { Value = mappedResults });
    }
}
