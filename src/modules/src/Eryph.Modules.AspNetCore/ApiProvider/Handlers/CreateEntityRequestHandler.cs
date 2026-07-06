using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

internal class CreateEntityRequestHandler<TEntity>(
    IOperationDispatcher operationDispatcher,
    IEndpointResolver endpointResolver,
    IMapper mapper,
    IUserRightsProvider userRightsProvider,
    IHttpContextAccessor httpContextAccessor)
    : ICreateEntityRequestHandler<TEntity>
{
    public async Task<ActionResult<Operation>> HandleOperationRequest(
        Func<object> createOperationFunc,
        CancellationToken cancellationToken)
    {
        // Honour a client-aborted request before we create and dispatch an operation.
        cancellationToken.ThrowIfCancellationRequested();

        var command = createOperationFunc();

        // StartNew persists and commits the operation (OperationManager.GetOrCreateAsync) before it
        // dispatches the command, so the row is durable before the controller can receive it. No
        // ambient TransactionScope: an enlisted MariaDB write commits via XA only at scope disposal,
        // which would let the dispatched command race ahead of the commit.
        var operation = await operationDispatcher.StartNew(
            userRightsProvider.GetUserTenantId(),
            httpContextAccessor.HttpContext?.TraceIdentifier ?? "",
            command,
            userRightsProvider.GetUserId());

        var operationModel = ((StateDb.Workflows.Operation)operation).Model;
        var mappedModel = mapper.Map<Operation>(operationModel);
        var operationUri = new Uri(endpointResolver.GetEndpoint("compute") + $"/v1/operations/{operationModel.Id}");

        return new AcceptedResult(operationUri, mappedModel);
    }
}
