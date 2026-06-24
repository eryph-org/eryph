#nullable enable
using System;
using System.Security.Cryptography;
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

public class EnrollEndpointTests
{
    private static string NewPublicKey()
    {
        using var key = RSA.Create(2048);
        return Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    private static Enroll CreateEndpoint(IComponentEnrollmentService service) =>
        new(service)
        {
            ProblemDetailsFactory = new StubProblemDetailsFactory(),
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static EnrollComponentRequest ValidRequest() =>
        new()
        {
            ComponentType = ComponentType.Controller,
            Fqdn = "controller1.eryph.local",
            PublicKey = NewPublicKey(),
            Token = "enroll-token",
        };

    [Fact]
    public async Task HandleAsync_returns_ok_with_the_mapped_enrollment_result_on_success()
    {
        var componentId = Guid.NewGuid();
        var endpoint = CreateEndpoint(new StubEnrollmentService(
            new ComponentEnrollmentResult { ComponentId = componentId, Certificate = [9, 8, 7] }));

        var action = await endpoint.HandleAsync(ValidRequest());

        var enrolled = action.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<EnrolledComponent>().Subject;
        enrolled.ComponentId.Should().Be(componentId.ToString());
        enrolled.Certificate.Should().Be(Convert.ToBase64String([9, 8, 7]));
    }

    [Fact]
    public async Task HandleAsync_returns_401_problem_when_enrollment_is_rejected()
    {
        var endpoint = CreateEndpoint(new StubEnrollmentService(rejected: true));

        var action = await endpoint.HandleAsync(ValidRequest());

        var problem = action.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        // Opaque: the detail must not reveal which check failed.
        problem.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("The component enrollment request was not authorized.");
    }

    [Fact]
    public async Task HandleAsync_returns_400_for_an_invalid_request_without_calling_the_service()
    {
        var service = new StubEnrollmentService(rejected: true);
        var endpoint = CreateEndpoint(service);
        var request = ValidRequest();
        request.PublicKey = "not-base64!!";   // invalid → validation fails

        var action = await endpoint.HandleAsync(request);

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        service.Called.Should().BeFalse("a malformed request must be rejected before reaching the service");
    }

    private sealed class StubEnrollmentService : IComponentEnrollmentService
    {
        private readonly ComponentEnrollmentResult? _result;
        private readonly bool _rejected;
        public bool Called { get; private set; }

        public StubEnrollmentService(ComponentEnrollmentResult result) => _result = result;
        public StubEnrollmentService(bool rejected) => _rejected = rejected;

        public Task<ComponentEnrollmentResult> EnrollAsync(
            ComponentEnrollmentRequest request, CancellationToken cancellationToken = default)
        {
            Called = true;
            return _rejected || _result is null
                ? throw new ComponentEnrollmentException("not authorized")
                : Task.FromResult(_result);
        }

        public Task<ComponentEnrollmentResult> RenewAsync(
            System.Security.Cryptography.X509Certificates.X509Certificate2 clientCertificate,
            ComponentEnrollmentRequest request, CancellationToken cancellationToken = default) =>
            throw new System.NotSupportedException("The enroll endpoint tests do not exercise renewal.");
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
