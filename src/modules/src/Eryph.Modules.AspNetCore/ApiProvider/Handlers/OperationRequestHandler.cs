using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Rebus.TransactionScopes;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public class OperationRequestHandler<TEntity>(
    IEndpointResolver endpointResolver,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper,
    IOperationDispatcher operationDispatcher,
    IUserRightsProvider userRightsProvider)
    : IOperationRequestHandler<TEntity> where TEntity : class
{
    public async Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(
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

        // TODO Do we need stateStore.SaveChanges() ?

        ta.Complete();

        return new AcceptedResult(operationUri, new ListResponse<Operation>()) { Value = mappedModel };
    }
}
