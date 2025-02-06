using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

[Route("v{version:apiVersion}")]
public class ValidateConfig()
    : EndpointBaseAsync
        .WithRequest<ValidateProjectNetworksConfigRequest>
        .WithActionResult<ProjectNetworksConfigValidationResult>
{
    [Authorize(Policy = "compute:projects:read")]
    [HttpPost("virtualnetworks/config/validate")]
    [SwaggerOperation(
        Summary = "Validate virtual networks config",
        Description = "Performs a quick validation of the virtual networks configuration",
        OperationId = "VirtualNetworks_ValidateConfig",
        Tags = ["Virtual Networks"])
    ]
    public override Task<ActionResult<ProjectNetworksConfigValidationResult>> HandleAsync(
        ValidateProjectNetworksConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = RequestValidations.ValidateProjectNetworkConfig(
            request.Configuration);

        var errors = validation.FailToSeq()
            .Map(e => new ValidationIssue { Member = e.Member, Message = e.Message })
            .ToList();

        return Task.FromResult(new ActionResult<ProjectNetworksConfigValidationResult>(new ProjectNetworksConfigValidationResult
        {
            IsValid = validation.IsSuccess,
            Errors = errors.Count > 0 ? errors : null,
        }));
    }
}
