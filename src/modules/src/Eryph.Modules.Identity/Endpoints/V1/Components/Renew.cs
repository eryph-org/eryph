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
/// Component certificate renewal. Authenticated by the component's CURRENT certificate (mutual TLS),
/// not a one-time token: a component presents its still-valid, CA-issued certificate and receives a
/// fresh one for the same identity (taken from the certificate, never the request). Anonymous to
/// bearer auth — the proof of identity is the client certificate, validated by the service.
/// </summary>
[Route("v{version:apiVersion}")]
public class Renew(IComponentEnrollmentService enrollmentService)
    : EndpointBaseAsync
        .WithRequest<EnrollComponentRequest>
        .WithActionResult<EnrolledComponent>
{
    [AllowAnonymous]
    [HttpPost("components/renew")]
    [SwaggerOperation(
        Summary = "Renew a component certificate",
        Description = "Issues fresh mTLS and (optionally) server-TLS certificates for the component identified by the presented client certificate.",
        OperationId = "Components_Renew",
        Tags = ["Components"])]
    [SwaggerResponse(Status200OK, "Success", typeof(EnrolledComponent), ["application/json"])]
    [SwaggerResponse(Status400BadRequest, "The renewal request is invalid")]
    [SwaggerResponse(Status401Unauthorized, "The renewal request was not authorized")]
    public override async Task<ActionResult<EnrolledComponent>> HandleAsync(
        [FromBody] EnrollComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the request shape (key encoding, server-DNS-name caps) before doing any work — the
        // same bounds the enroll endpoint applies, minus the token (renewal authenticates with the
        // certificate). Not sensitive, so a detailed 400 is fine.
        var validation = ComponentEnrollmentValidations.ValidateForRenewal(request);
        if (validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        var clientCertificate = HttpContext.Connection.ClientCertificate;
        if (clientCertificate is null)
            return Problem(
                statusCode: Status401Unauthorized,
                detail: "Renewal requires the component's current client certificate.");

        try
        {
            using (clientCertificate)
            {
                var result = await enrollmentService.RenewAsync(
                    clientCertificate, request.ToServiceRequest(), cancellationToken);
                return Ok(result.ToApiModel());
            }
        }
        catch (ComponentEnrollmentException)
        {
            return Problem(
                statusCode: Status401Unauthorized,
                detail: "The component renewal request was not authorized.");
        }
    }
}
