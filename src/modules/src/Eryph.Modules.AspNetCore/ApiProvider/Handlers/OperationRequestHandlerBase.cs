using System;
using System.Threading.Tasks;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.StateDb.Workflows;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public abstract class OperationRequestHandlerBase(
    IApiResultFactory resultFactory,
    IEndpointResolver endpointResolver,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper,
    IOperationDispatcher operationDispatcher,
    IUserRightsProvider userRightsProvider)
{
    /// <summary>
    /// Creates a response with <see cref="ProblemDetails"/> in the same
    /// way as <see cref="ControllerBase.Problem"/> does.
    /// </summary>
    protected ObjectResult Problem(int statusCode, string detail)
    {
        return resultFactory.Problem(statusCode, detail);
    }

    protected async Task<ActionResult> StartOperation(object command)
    {
        var operation = await operationDispatcher.StartNew(
            userRightsProvider.GetUserTenantId(),
            httpContextAccessor.HttpContext?.TraceIdentifier ?? "",
            command,
            userRightsProvider.GetUserId());

        var operationModel = ((Operation)operation).Model;
        var mappedModel = mapper.Map<Model.V1.Operation>(operationModel);
        var operationUri = new Uri(endpointResolver.GetEndpoint("compute") + $"/v1/operations/{operationModel.Id}");

        return new AcceptedResult(operationUri, mappedModel);
    }
}
