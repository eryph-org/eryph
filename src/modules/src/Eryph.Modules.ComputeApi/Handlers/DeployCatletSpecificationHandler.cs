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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Handlers;

public class DeployCatletSpecificationHandler(
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

        return await base.ValidateRequest(model, cancellationToken);
    }
}
