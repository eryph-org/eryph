using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt.Pipes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Eryph.Modules.ComputeApi.Handlers;

public class DeployCatletSpecificationHandler(
    IApiResultFactory apiResultFactory,
    IOperationDispatcher operationDispatcher,
    IStateStoreRepository<Catlet> catletRepository,
    IStateStoreRepository<CatletSpecification> specificationRepository,
    IStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
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
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"The catlet specification is already deployed as catlet {catlet.Id}. Please remove the catlet before deploying a new version.");

        var catletWithNameExists = await catletRepository.AnyAsync(
            new CatletSpecs.GetByName(model.Name, model.ProjectId),
            cancellationToken);

        if (catletWithNameExists)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A catlet with the name '{model.Name}' already exists in the project '{model.Project.Name}'. Catlet names must be unique within a project.");


        var specificationVersion = await specificationVersionRepository.GetBySpecAsync(
            new CatletSpecificationVersionSpecs.GetLatestBySpecificationIdReadOnly(model.Id),
            cancellationToken);
        if (specificationVersion is null)
        {
            return ValidationProblem(
                detail: "The catlet specification has no deployable version.",
                new ModelStateDictionary());
        }

        return await base.ValidateRequest(model, cancellationToken);
    }
}
