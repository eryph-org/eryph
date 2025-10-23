using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Handlers;

public class DeleteCatletSpecificationHandler(
    IApiResultFactory apiResultFactory,
    IOperationDispatcher operationDispatcher,
    IStateStoreRepository<Catlet> catletRepository,
    IStateStoreRepository<CatletSpecification> specificationRepository,
    IEndpointResolver endpointResolver,
    IMapper mapper,
    IUserRightsProvider userRightsProvider,
    IHttpContextAccessor httpContextAccessor)
    : EntityOperationRequestHandler<CatletSpecification>(
        apiResultFactory,
        endpointResolver,
        httpContextAccessor,
        mapper,
        operationDispatcher,
        specificationRepository,
        userRightsProvider)
{
    protected override async Task<ActionResult?> ValidateRequest(
        CatletSpecification model,
        CancellationToken cancellationToken = default)
    {
        var catlet = await catletRepository.GetBySpecAsync(
            new CatletSpecs.GetBySpecificationId(model.Id),
            cancellationToken);
        if (catlet is not null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The catlet specification is deployed as a catlet. Please delete the catlet first.");
        }

        return await base.ValidateRequest(model, cancellationToken);
    }
}
