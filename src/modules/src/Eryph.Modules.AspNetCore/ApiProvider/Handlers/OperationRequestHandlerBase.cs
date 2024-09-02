using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public abstract class OperationRequestHandlerBase(
    IEndpointResolver endpointResolver,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper,
    ProblemDetailsFactory problemDetailsFactory,
    IOperationDispatcher operationDispatcher,
    IUserRightsProvider userRightsProvider)
{
    /// <summary>
    /// Creates a response with <see cref="ProblemDetails"/> in the same
    /// way as <see cref="ControllerBase.Problem"/> does.
    /// </summary>
    protected ObjectResult Problem(int statusCode, string detail)
    {
        var httpContext = httpContextAccessor.HttpContext
                          ?? throw new InvalidOperationException("HttpContext is not available.");

        var problemDetails = problemDetailsFactory.CreateProblemDetails(
            httpContext,
            statusCode: statusCode,
            detail: detail);

        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status,
        };
    }

    protected async Task<ActionResult> StartOperation(object command)
    {
        var operation = await operationDispatcher.StartNew(
            userRightsProvider.GetUserTenantId(),
            httpContextAccessor.HttpContext?.TraceIdentifier ?? "",
            command);
        var operationModel = (operation as StateDb.Workflows.Operation)?.Model;

        if (operationModel == null)
            return new UnprocessableEntityResult();

        var mappedModel = mapper.Map<Operation>(operationModel);
        var operationUri = new Uri(endpointResolver.GetEndpoint("common") + $"/v1/operations/{operationModel.Id}");
        return new AcceptedResult(operationUri, new ListResponse<Operation>()) { Value = mappedModel };
    }
}
