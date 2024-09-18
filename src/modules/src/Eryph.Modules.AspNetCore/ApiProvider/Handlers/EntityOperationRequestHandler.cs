using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Ardalis.Specification;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Rebus.TransactionScopes;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public class EntityOperationRequestHandler<TEntity>(
    IOperationDispatcher operationDispatcher,
    IStateStoreRepository<TEntity> repository,
    IEndpointResolver endpointResolver,
    IMapper mapper,
    IUserRightsProvider userRightsProvider,
    IHttpContextAccessor httpContextAccessor,
    ProblemDetailsFactory problemDetailsFactory)
    : OperationRequestHandlerBase(
            endpointResolver,
            httpContextAccessor,
            mapper,
            problemDetailsFactory,
            operationDispatcher,
            userRightsProvider),
        IEntityOperationRequestHandler<TEntity>
    where TEntity : class
{
    private readonly IUserRightsProvider _userRightsProvider = userRightsProvider;

    public async Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(
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

        var validationResult = ValidateRequest(model);
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
    /// <param name="model"></param>
    /// <returns></returns>
    protected virtual ActionResult? ValidateRequest(TEntity model) => null;
}
