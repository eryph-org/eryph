using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Ardalis.Specification;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Rebus.TransactionScopes;

using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public class EntityOperationRequestHandler<TEntity>(
    IApiResultFactory apiResultFactory,
    IEndpointResolver endpointResolver,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper,
    IOperationDispatcher operationDispatcher,
    IStateStoreRepository<TEntity> repository,
    IUserRightsProvider userRightsProvider)
    : OperationRequestHandlerBase(
            apiResultFactory,
            endpointResolver,
            httpContextAccessor,
            mapper,
            operationDispatcher,
            userRightsProvider),
        IEntityOperationRequestHandler<TEntity>
    where TEntity : class
{
    private readonly IUserRightsProvider _userRightsProvider = userRightsProvider;

    public async Task<ActionResult<Operation>> HandleOperationRequest(
        Func<ISingleResultSpecification<TEntity>?> specificationFunc,
        Func<TEntity, object> createOperationFunc,
        CancellationToken cancellationToken)
    {
        using var ta = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
        ta.EnlistRebus();
 
        var spec = specificationFunc();
        var model = spec != null ? await repository.GetBySpecAsync(spec, cancellationToken) : null;

        switch (model)
        {
            case null:
                return new NotFoundResult();
            case Gene _ when !(await _userRightsProvider.HasDefaultTenantAccess(AccessRight.Admin)):
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "You do not have super admin access.");
            case Resource resource when !(await _userRightsProvider.HasResourceAccess(resource.Id, AccessRight.Write)):
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "You do not have write access to the project.");
            case Project project when !(await _userRightsProvider.HasProjectAccess(project.Id, AccessRight.Admin)):
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "You do not have admin access to the project.");
            case ProjectRoleAssignment roleAssignment when !(await _userRightsProvider.HasProjectAccess(roleAssignment.ProjectId, AccessRight.Admin)):
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "You do not have admin access to the project.");
        }

        var validationResult = await ValidateRequest(model, cancellationToken);
        if (validationResult is not null)
            return validationResult;

        var command = createOperationFunc(model);
        var result = await StartOperation(command);

        await repository.SaveChangesAsync(cancellationToken);
        ta.Complete();

        return result;
    }

    /// <summary>
    /// Override this method to perform addition validation based on the loaded model.
    /// </summary>
    protected virtual Task<ActionResult?> ValidateRequest(
        TEntity model,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ActionResult?>(null);
}
