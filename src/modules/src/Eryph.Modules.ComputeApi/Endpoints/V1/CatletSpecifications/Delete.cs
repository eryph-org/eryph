using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Modules.ComputeApi.Handlers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class Delete : ResourceOperationEndpoint<DeleteCatletSpecificationRequest, CatletSpecification>
{
    private readonly IReadonlyStateStoreRepository<Catlet> _catletRepository;
    private readonly IReadonlyStateStoreRepository<CatletSpecification> _specificationRepository;
    private readonly ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> _specBuilder;

    public Delete(
        IEntityOperationRequestHandler<CatletSpecification> operationHandler,
        IReadonlyStateStoreRepository<Catlet> catletRepository,
        IReadonlyStateStoreRepository<CatletSpecification> specificationRepository,
        ISingleEntitySpecBuilder<SingleEntityRequest, CatletSpecification> specBuilder) : base(operationHandler, specBuilder)
    {
        _catletRepository = catletRepository;
        _specificationRepository = specificationRepository;
        _specBuilder = specBuilder;
    }

    protected override object CreateOperationMessage(
        CatletSpecification model,
        DeleteCatletSpecificationRequest request)
    {
        return new DestroyCatletSpecificationCommand
        {
            SpecificationId = model.Id,
            DestroyCatlet = request.Body.DeleteCatlet.GetValueOrDefault(),
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpDelete("catlet_specifications/{id}")]
    [SwaggerOperation(
        Summary = "Delete a catlet specification",
        Description = "Deletes a catlet specification",
        OperationId = "CatletSpecifications_Delete",
        Tags = ["Catlet Specifications"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] DeleteCatletSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var spec = _specBuilder.GetSingleEntitySpec(request, AccessRight.Write);
        if (spec is null)
            return NotFound();

        var catletSpecification = await _specificationRepository.GetBySpecAsync(spec, cancellationToken);
        if (catletSpecification is null)
            return NotFound();

        if (request.Body.DeleteCatlet.GetValueOrDefault())
            return await base.HandleAsync(request, cancellationToken);
        
        var catletExists = await _catletRepository.AnyAsync(
            new CatletSpecs.GetBySpecificationId(catletSpecification.Id),
            cancellationToken);
        if (catletExists)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The catlet specification is deployed as a catlet. Please delete the catlet first.");
        }

        return await base.HandleAsync(request, cancellationToken);
    }
}
