#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity.Endpoints.Components;
using Eryph.Modules.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Eryph.Modules.Identity.Test.Endpoints.Components;

public class EnrollEndpointTests
{
    [Fact]
    public async Task HandleAsync_returns_ok_with_the_enrollment_result_on_success()
    {
        var expected = new ComponentEnrollmentResult { ComponentId = Guid.NewGuid() };
        var endpoint = new Enroll(new StubEnrollmentService(expected));

        var action = await endpoint.HandleAsync(new ComponentEnrollmentRequest());

        action.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task HandleAsync_returns_unauthorized_when_enrollment_is_rejected()
    {
        var endpoint = new Enroll(new StubEnrollmentService(rejected: true));

        var action = await endpoint.HandleAsync(new ComponentEnrollmentRequest());

        action.Result.Should().BeOfType<UnauthorizedResult>();
    }

    private sealed class StubEnrollmentService : IComponentEnrollmentService
    {
        private readonly ComponentEnrollmentResult? _result;
        private readonly bool _rejected;

        public StubEnrollmentService(ComponentEnrollmentResult result) => _result = result;
        public StubEnrollmentService(bool rejected) => _rejected = rejected;

        public ComponentEnrollmentResult Enroll(ComponentEnrollmentRequest request) =>
            _rejected || _result is null
                ? throw new ComponentEnrollmentException("not authorized")
                : _result;
    }
}
