using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.Identity.Endpoints.V1.Components;

/// <summary>
/// Component enrollment endpoint. A component presents its identity claim, public key(s) and a
/// one-time enrollment token (off the bus, over HTTPS); the configured enrollment policy authorizes
/// the request and the component CA issues its certificates. Authentication here is the enrollment
/// token, not a bearer token (the component has none yet), so the endpoint is anonymous.
/// </summary>
[Route("v{version:apiVersion}")]
public class Enroll(IComponentEnrollmentService enrollmentService)
    : EndpointBaseAsync.WithRequest<EnrollComponentRequest>.WithActionResult<EnrolledComponent>
{
    // SECURITY: this anonymous endpoint authenticates only via the enrollment token. Before it is
    // exposed on a reachable interface, the host should rate-limit this route to bound enumeration
    // attempts (a holder of a stolen token probing types/FQDNs) and log flooding.
    [AllowAnonymous]
    [HttpPost("components/enroll")]
    [SwaggerOperation(
        Summary = "Enroll a component",
        Description =
            "Issues mTLS and (optionally) server-TLS certificates for a component after enrollment-token authorization.",
        OperationId = "Components_Enroll",
        Tags = ["Components"])]
    [SwaggerResponse(Status200OK, "Success", typeof(EnrolledComponent), "application/json")]
    [SwaggerResponse(Status400BadRequest, "The enrollment request is invalid")]
    [SwaggerResponse(Status401Unauthorized, "The enrollment request was not authorized")]
    public override async Task<ActionResult<EnrolledComponent>> HandleAsync(
        [FromBody] EnrollComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the request shape first (not sensitive) so a malformed request returns a detailed
        // 400 without ever redeeming the one-time token.
        var validation = ComponentEnrollmentValidations.Validate(request);
        if (validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        try
        {
            var result = await enrollmentService.EnrollAsync(request.ToServiceRequest(), cancellationToken);
            return Ok(result.ToApiModel());
        }
        catch (ComponentEnrollmentException)
        {
            // Respond uniformly so the endpoint does not reveal whether the token, the bound type/host,
            // or the public key was the problem.
            return Problem(
                statusCode: Status401Unauthorized,
                detail: "The component enrollment request was not authorized.");
        }
    }
}
