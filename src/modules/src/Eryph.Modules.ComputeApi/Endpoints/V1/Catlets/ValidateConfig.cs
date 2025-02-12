﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

[Route("v{version:apiVersion}")]
public class ValidateConfig()
    : EndpointBaseAsync
        .WithRequest<ValidateConfigRequest>
        .WithActionResult<CatletConfigValidationResult>
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpPost("catlets/config/validate")]
    [SwaggerOperation(
        Summary = "Validate catlet config",
        Description = "Performs a quick validation of the catlet configuration",
        OperationId = "Catlets_ValidateConfig",
        Tags = ["Catlets"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(CatletConfigValidationResult),
        contentTypes: ["application/json"])
    ]
    public override Task<ActionResult<CatletConfigValidationResult>> HandleAsync(
        [FromBody] ValidateConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = RequestValidations.ValidateCatletConfig(
            request.Configuration);

        var errors = validation.FailToSeq()
            .Map(e => new ValidationIssue { Member = e.Member, Message = e.Message })
            .ToList();

        return Task.FromResult(new ActionResult<CatletConfigValidationResult>(new CatletConfigValidationResult
        {
            IsValid = validation.IsSuccess,
            Errors = errors.Count > 0 ? errors : null,
        }));
    }
}
