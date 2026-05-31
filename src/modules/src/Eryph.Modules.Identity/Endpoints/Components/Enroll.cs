using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.Identity.Endpoints.Components;

/// <summary>
/// Anonymous component enrollment endpoint. A component presents its identity claim, public key and
/// an enrollment credential (off the bus, over HTTPS); the configured enrollment policy authorizes
/// the request and the component CA issues its mTLS certificate. Authentication here is the
/// enrollment policy, not a bearer token (the component has none yet).
/// </summary>
public class Enroll(IComponentEnrollmentService enrollmentService)
    : EndpointBaseAsync
        .WithRequest<ComponentEnrollmentRequest>
        .WithActionResult<ComponentEnrollmentResult>
{
    [AllowAnonymous]
    [HttpPost("~/components/enroll")]
    [SwaggerOperation(
        Summary = "Enroll a component",
        Description = "Issues an mTLS certificate for a component after enrollment-policy authorization.",
        OperationId = "Components_Enroll",
        Tags = ["Components"])]
    [SwaggerResponse(Status200OK, "Success", typeof(ComponentEnrollmentResult), ["application/json"])]
    [SwaggerResponse(Status401Unauthorized, "The enrollment request was not authorized")]
    public override Task<ActionResult<ComponentEnrollmentResult>> HandleAsync(
        [FromBody] ComponentEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = enrollmentService.Enroll(request);
            return Task.FromResult<ActionResult<ComponentEnrollmentResult>>(Ok(result));
        }
        catch (ComponentEnrollmentException)
        {
            // Respond uniformly so the endpoint does not reveal whether the credential, the
            // identity claim, or the public key was the problem.
            return Task.FromResult<ActionResult<ComponentEnrollmentResult>>(Unauthorized());
        }
    }
}
