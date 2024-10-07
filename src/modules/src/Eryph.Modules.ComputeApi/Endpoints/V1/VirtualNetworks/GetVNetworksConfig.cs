using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class GetVNetworksConfig(
    IGetRequestHandler<Project, VirtualNetworkConfiguration> requestHandler,
    IUserRightsProvider userRightsProvider)
    : SingleResultEndpoint<ProjectRequest, VirtualNetworkConfiguration, Project>(requestHandler)
{
    protected override ISingleResultSpecification<Project>? CreateSpecification(ProjectRequest request)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            return null;

        var sufficientRoles = userRightsProvider.GetProjectRoles(AccessRight.Read);

        return new ProjectSpecs.GetById(
            projectId,
            userRightsProvider.GetAuthContext(),
            sufficientRoles);
    }

    [Authorize(Policy = "compute:projects:read")]
    // ReSharper disable once StringLiteralTypo
    [HttpGet("projects/{projectId}/virtualnetworks/config")]
    [SwaggerOperation(
        Summary = "Get project virtual networks configuration",
        Description = "Get the configuration for all networks in a project",
        OperationId = "VirtualNetworks_GetConfig",
        Tags = ["Virtual Networks"])
    ]
    [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(VirtualNetworkConfiguration))]
    public override Task<ActionResult<VirtualNetworkConfiguration>> HandleAsync(
        [FromRoute] ProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return base.HandleAsync(request, cancellationToken);
    }
}
