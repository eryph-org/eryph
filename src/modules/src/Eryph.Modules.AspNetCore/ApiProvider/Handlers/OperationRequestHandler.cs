using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public class OperationRequestHandler<TEntity>(
    IApiResultFactory apiResultFactory,
    IEndpointResolver endpointResolver,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper,
    IOperationDispatcher operationDispatcher,
    IUserRightsProvider userRightsProvider)
    : OperationRequestHandlerBase(
            apiResultFactory,
            endpointResolver,
            httpContextAccessor,
            mapper,
            operationDispatcher,
            userRightsProvider),
        IOperationRequestHandler<TEntity> where TEntity : class
{
    private readonly IUserRightsProvider _userRightsProvider = userRightsProvider;

    public async Task<ActionResult<Operation>> HandleOperationRequest(
        Func<object> createOperationFunc,
        CancellationToken cancellationToken)
    {
        // Honour a client-aborted request before we create and dispatch an operation.
        cancellationToken.ThrowIfCancellationRequested();

        if (typeof(TEntity) == typeof(Gene))
        {
            if (!await _userRightsProvider.HasDefaultTenantAccess(AccessRight.Admin))
                return Problem(
                    StatusCodes.Status403Forbidden,
                    "You do not have super admin access.");
        }
        else if (typeof(TEntity) == typeof(CatletSpecificationVersion))
        {
            // We do not perform a permission check as we do not have the necessary
            // information. The endpoint must perform the permission check itself.
        }
        else
        {
            return new NotFoundResult();
        }

        var command = createOperationFunc();
        // StartNew persists and commits the operation (OperationManager.GetOrCreateAsync) before it
        // dispatches the command, so the row is durable before the controller can receive it. No
        // ambient TransactionScope: an enlisted MariaDB write commits via XA only at scope disposal,
        // which would let the dispatched command race ahead of the commit.
        var result = await StartOperation(command);

        return result;
    }
}
