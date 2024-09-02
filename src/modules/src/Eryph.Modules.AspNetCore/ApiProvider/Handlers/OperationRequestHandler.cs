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
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Rebus.TransactionScopes;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public class OperationRequestHandler<TEntity>(
    IEndpointResolver endpointResolver,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper,
    IOperationDispatcher operationDispatcher,
    ProblemDetailsFactory problemDetailsFactory,
    IStateStore stateStore,
    IUserRightsProvider userRightsProvider)
    : OperationRequestHandlerBase(
            endpointResolver,
            httpContextAccessor,
            mapper,
            problemDetailsFactory,
            operationDispatcher,
            userRightsProvider),
        IOperationRequestHandler<TEntity> where TEntity : class
{
    private readonly IUserRightsProvider _userRightsProvider = userRightsProvider;

    public async Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(
        Func<object> createOperationFunc,
        CancellationToken cancellationToken)
    {
        using var ta = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
        ta.EnlistRebus();

        // TODO Should we use a subclass instead
        if (typeof(TEntity) == typeof(Gene)
            && !await _userRightsProvider.HasDefaultTenantAccess(AccessRight.Admin))
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have super admin access.");
        }

        var command = createOperationFunc();
        var result = await StartOperation(command);

        await stateStore.SaveChangesAsync(cancellationToken);
        ta.Complete();

        return result;
    }
}
