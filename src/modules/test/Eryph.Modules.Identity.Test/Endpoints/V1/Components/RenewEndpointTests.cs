#nullable enable
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity.Endpoints.V1.Components;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Eryph.Modules.Identity.Test.Endpoints.V1.Components;

public class RenewEndpointTests
{
    private static string NewPublicKey()
    {
        using var key = RSA.Create(2048);
        return Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    private static X509Certificate2 NewClientCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=agent1.eryph.local", key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static Renew CreateEndpoint(IComponentEnrollmentService service, X509Certificate2? clientCertificate)
    {
        var httpContext = new DefaultHttpContext();
        if (clientCertificate is not null)
            httpContext.Connection.ClientCertificate = clientCertificate;
        return new Renew(service)
        {
            ProblemDetailsFactory = new StubProblemDetailsFactory(),
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private static EnrollComponentRequest ValidRequest() =>
        new()
        {
            ComponentType = ComponentType.VMHostAgent,
            Fqdn = "agent1.eryph.local",
            PublicKey = NewPublicKey(),
            // Token is a required member of the wire model but is ignored on renewal (the client
            // certificate authenticates); send it empty.
            Token = "",
        };

    [Fact]
    public async Task HandleAsync_returns_401_when_no_client_certificate_is_presented()
    {
        var service = new StubRenewalService();
        var endpoint = CreateEndpoint(service, null);

        var action = await endpoint.HandleAsync(ValidRequest());

        action.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        service.Called.Should().BeFalse("renewal requires the client certificate before reaching the service");
    }

    [Fact]
    public async Task HandleAsync_returns_400_for_an_invalid_request_without_calling_the_service()
    {
        var service = new StubRenewalService();
        using var cert = NewClientCertificate();
        var endpoint = CreateEndpoint(service, cert);
        var request = ValidRequest();
        request.PublicKey = "not-base64!!";

        var action = await endpoint.HandleAsync(request);

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        service.Called.Should().BeFalse("a malformed request must be rejected before reaching the service");
    }

    [Fact]
    public async Task HandleAsync_returns_ok_with_the_mapped_result_on_success()
    {
        var componentId = Guid.NewGuid();
        using var cert = NewClientCertificate();
        var endpoint = CreateEndpoint(
            new StubRenewalService(new ComponentEnrollmentResult
                { ComponentId = componentId, Certificate = [4, 5, 6] }),
            cert);

        var action = await endpoint.HandleAsync(ValidRequest());

        var renewed = action.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<EnrolledComponent>().Subject;
        renewed.ComponentId.Should().Be(componentId.ToString());
    }

    [Fact]
    public async Task HandleAsync_returns_401_problem_when_renewal_is_rejected()
    {
        using var cert = NewClientCertificate();
        var endpoint = CreateEndpoint(new StubRenewalService(true), cert);

        var action = await endpoint.HandleAsync(ValidRequest());

        var problem = action.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        // Opaque detail: must not reveal which check failed.
        problem.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("The component renewal request was not authorized.");
    }

    private sealed class StubRenewalService : IComponentEnrollmentService
    {
        private readonly bool _rejected;
        private readonly ComponentEnrollmentResult? _result;

        public StubRenewalService(ComponentEnrollmentResult result) => _result = result;
        public StubRenewalService(bool rejected = false) => _rejected = rejected;
        public bool Called { get; private set; }

        public Task<ComponentEnrollmentResult> RenewAsync(
            X509Certificate2 clientCertificate, ComponentEnrollmentRequest request,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            return _rejected || _result is null
                ? throw new ComponentEnrollmentException("not authorized")
                : Task.FromResult(_result);
        }

        public Task<ComponentEnrollmentResult> EnrollAsync(
            ComponentEnrollmentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The renew endpoint tests do not exercise enrollment.");
    }

    private sealed class StubProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext, int? statusCode = null, string? title = null,
            string? type = null, string? detail = null, string? instance = null) =>
            new() { Status = statusCode, Title = title, Detail = detail };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null,
            string? title = null, string? type = null, string? detail = null, string? instance = null) =>
            new(modelStateDictionary) { Status = statusCode ?? StatusCodes.Status400BadRequest };
    }
}
