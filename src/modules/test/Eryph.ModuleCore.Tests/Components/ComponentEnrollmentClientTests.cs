using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Eryph.ModuleCore.Tests.Components;

public class ComponentEnrollmentClientTests
{
    private static ComponentEnrollmentClient Create(IEnrollmentTransport transport, IComponentCertificateStore store) =>
        new(transport, store,
            new ComponentIdentity(ComponentType.VMHostAgent, "q"),
            new ComponentEnrollmentClientOptions
            {
                EnrollmentSecret = "secret",
                InitialRetryDelay = TimeSpan.Zero,
                MaxRetryDelay = TimeSpan.Zero,
            },
            NullLogger<ComponentEnrollmentClient>.Instance);

    [Fact]
    public async Task EnsureEnrolledAsync_retries_until_the_identity_service_is_available()
    {
        var transport = new FakeTransport(failuresBeforeSuccess: 2);
        var store = new FakeStore();

        await Create(transport, store).EnsureEnrolledAsync(CancellationToken.None);

        // Two failures (identity still starting), then success — and the result is persisted once.
        transport.Calls.Should().Be(3);
        store.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task EnsureEnrolledAsync_does_not_enroll_when_a_current_certificate_exists()
    {
        var transport = new FakeTransport(failuresBeforeSuccess: 0);
        var store = new FakeStore { Current = true };

        await Create(transport, store).EnsureEnrolledAsync(CancellationToken.None);

        transport.Calls.Should().Be(0);
        store.SaveCount.Should().Be(0);
    }

    private sealed class FakeTransport(int failuresBeforeSuccess) : IEnrollmentTransport
    {
        public int Calls { get; private set; }

        public Task<ComponentEnrollmentResult> EnrollAsync(
            ComponentEnrollmentRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls <= failuresBeforeSuccess)
                throw new InvalidOperationException("the identity service is not ready");

            return Task.FromResult(new ComponentEnrollmentResult
            {
                ComponentId = Guid.NewGuid(),
                Certificate = [1],
                IssuingChain = [[2]],
                CaTrustBundle = [[3]],
            });
        }
    }

    private sealed class FakeStore : IComponentCertificateStore
    {
        public bool Current { get; init; }
        public int SaveCount { get; private set; }

        public bool HasCurrentCertificate() => Current;
        public bool HasValidCertificate() => Current;
        public void Save(byte[] pkcs8PrivateKey, ComponentEnrollmentResult result) => SaveCount++;

        public System.Security.Cryptography.X509Certificates.X509Certificate2? LoadClientCertificate() => null;

        public string? GetClientCertificatePfxPath() => Current ? "fake.pfx" : null;

        public System.Security.Cryptography.X509Certificates.X509Certificate2Collection LoadCaTrustBundle() => [];
    }
}
