using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Rebus.TransactionScopes;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

internal class CreateEntityRequestHandler<TEntity>(
    IOperationDispatcher operationDispatcher,
    IEndpointResolver endpointResolver,
    IMapper mapper,
    IUserRightsProvider userRightsProvider,
    IHttpContextAccessor httpContextAccessor,
    StateStoreContext dbContext)
    : ICreateEntityRequestHandler<TEntity>
{
    public async Task<ActionResult<Operation>> HandleOperationRequest(
        Func<object> createOperationFunc,
        CancellationToken cancellationToken)
    {
        using var ta = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
        ta.EnlistRebus();

        var command = createOperationFunc();

        var operation = await operationDispatcher.StartNew(
            userRightsProvider.GetUserTenantId(),
            httpContextAccessor.HttpContext?.TraceIdentifier ?? "",
            command);
        var operationModel = (operation as StateDb.Workflows.Operation)?.Model;

        if (operationModel == null)
            return new UnprocessableEntityResult();

        var mappedModel = mapper.Map<Operation>(operationModel);
        var operationUri = new Uri(endpointResolver.GetEndpoint("common") + $"/v1/operations/{operationModel.Id}");

        await dbContext.SaveChangesAsync(cancellationToken);
        ta.Complete();

        return new AcceptedResult(operationUri, mappedModel);
    }
}
