using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.ComputeApi.Model.V1;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class ListVersions(
    IMapper mapper,
    IReadonlyStateStoreRepository<StateDb.Model.CatletSpecification> specificationRepository,
    IReadonlyStateStoreRepository<StateDb.Model.CatletSpecificationVersion> specificationVersionRepository,
    IUserRightsProvider userRightsProvider)
    : EndpointBaseAsync
        .WithRequest<ListCatletSpecificationVersionsRequest>
        .WithActionResult<ListResponse<CatletSpecificationVersion>>
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("catlet_specifications/{specification_id}/versions")]
    [SwaggerOperation(
        Summary = "List all catlet specification versions",
        Description = "List all catlet specification versions",
        OperationId = "CatletSpecificationVersions_List",
        Tags = ["Catlet Specifications"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ListResponse<CatletSpecificationVersion>),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<ListResponse<CatletSpecificationVersion>>> HandleAsync(
        [FromRoute] ListCatletSpecificationVersionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.SpecificationId, out var specificationId))
            return new NotFoundResult();

        var specificationExists = await specificationRepository.AnyAsync(
            new ResourceSpecs<StateDb.Model.CatletSpecification>.GetById(
                specificationId,
                userRightsProvider.GetAuthContext(),
                userRightsProvider.GetResourceRoles<StateDb.Model.CatletSpecification>(StateDb.Model.AccessRight.Read)),
            cancellationToken);

        if (!specificationExists)
            return new NotFoundResult();

        var dpSpecificationVersions = await specificationVersionRepository.ListAsync(
            new CatletSpecificationVersionSpecs.ListBySpecificationIdReadOnly(specificationId),
            cancellationToken);

        var mappedResults = mapper.Map<IReadOnlyList<CatletSpecificationVersion>>(
            dpSpecificationVersions,
            o => o.SetAuthContext(userRightsProvider.GetAuthContext()));

        return new JsonResult(new ListResponse<CatletSpecificationVersion> { Value = mappedResults });
    }
}
