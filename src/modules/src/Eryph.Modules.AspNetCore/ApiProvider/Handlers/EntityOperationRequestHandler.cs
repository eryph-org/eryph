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

internal class EntityOperationRequestHandler<TEntity>(
    IOperationDispatcher operationDispatcher,
    IReadRepositoryBase<TEntity> repository,
    IEndpointResolver endpointResolver,
    IMapper mapper,
    IUserRightsProvider userRightsProvider,
    IHttpContextAccessor httpContextAccessor,
    ProblemDetailsFactory problemDetailsFactory,
    StateStoreContext dbContext)
    : IOperationRequestHandler<TEntity>
    where TEntity : class
{
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
            case Resource resource when !(await userRightsProvider.HasResourceAccess(resource.Id, AccessRight.Write)):
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "You do not have have write access to the project.");
            case Project project when !(await userRightsProvider.HasProjectAccess(project.Id, AccessRight.Admin)):
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "You do not have have admin access to the project.");
            case ProjectRoleAssignment roleAssignment when !(await userRightsProvider.HasProjectAccess(roleAssignment.ProjectId, AccessRight.Admin)):
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "You do not have have admin access to the project.");
        }

        var command = createOperationFunc(model);
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

        return new AcceptedResult(operationUri, new ListResponse<Operation>()){ Value = mappedModel };
    }

    /// <summary>
    /// Creates a response with <see cref="ProblemDetails"/> in the same
    /// way as <see cref="ControllerBase.Problem"/> does.
    /// </summary>
    private ObjectResult Problem(int statusCode, string detail)
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
}
