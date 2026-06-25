using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations;

// Like the other operations endpoints, this has no scope policy: authorization is
// enforced per-operation by GetByIdForCancellation (requester, project owner or super
// admin), which is stricter than a coarse scope check.
[Route("v{version:apiVersion}")]
public class Cancel(
    IStateStore stateStore,
    IUserRightsProvider userRightsProvider,
    IOperationCancellationDispatcher cancellationDispatcher)
    : EndpointBaseAsync.WithRequest<SingleEntityRequest>.WithActionResult
{
    [HttpPost("operations/{id}/cancel")]
    [SwaggerOperation(
            Summary = "Cancel an operation",
            Description = "Requests cancellation of a running operation. Best-effort: only tasks "
                          + "whose handlers support cancellation are interrupted.",
            OperationId = "Operations_Cancel",
            Tags = ["Operations"]),
    ]
    [SwaggerResponse(StatusCodes.Status202Accepted, "Success")]
    public override async Task<ActionResult> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out var operationId))
            return NotFound();

        // Authorize: only the operation's requester, an owner of all its projects,
        // or a super admin may cancel it. Returns null (-> 404) otherwise so the
        // existence of the operation is not revealed.
        var spec = new OperationSpecs.GetByIdForCancellation(
            operationId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetUserId(),
            userRightsProvider.GetProjectRoles(AccessRight.Admin));

        var operation = await stateStore.For<OperationModel>()
            .GetBySpecAsync(spec, cancellationToken);
        if (operation is null)
            return NotFound();

        await cancellationDispatcher.CancelOperation(operationId);

        return Accepted();
    }
}
