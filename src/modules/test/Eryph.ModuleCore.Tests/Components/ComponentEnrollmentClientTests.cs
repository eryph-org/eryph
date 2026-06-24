using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
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
                Token = "token",
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

    [Fact]
    public async Task EnsureEnrolledAsync_makes_a_single_non_blocking_attempt_when_a_valid_cert_is_in_its_renewal_window()
    {
        // Valid but not current (renewal window): the component can keep running, so a failed renewal
        // must not block/retry — it is left for the next check.
        var transport = new FakeTransport(failuresBeforeSuccess: 1);
        var store = new FakeStore { Valid = true, Current = false };

        await Create(transport, store).EnsureEnrolledAsync(CancellationToken.None);

        transport.Calls.Should().Be(1);
        store.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureEnrolledAsync_renews_a_certificate_in_its_renewal_window()
    {
        var transport = new FakeTransport(failuresBeforeSuccess: 0);
        var store = new FakeStore { Valid = true, Current = false };

        await Create(transport, store).EnsureEnrolledAsync(CancellationToken.None);

        transport.Calls.Should().Be(1);
        store.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task EnsureEnrolledAsync_fails_fast_on_a_non_transient_error_instead_of_retrying_forever()
    {
        // No valid certificate (blocking), but the failure is non-transient (a 401 from a used/expired
        // token): it must surface immediately rather than wedge startup in an infinite retry loop.
        var transport = new FakeTransport(
            failuresBeforeSuccess: int.MaxValue,
            failureFactory: () => new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized));
        var store = new FakeStore();

        var act = () => Create(transport, store).EnsureEnrolledAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        transport.Calls.Should().Be(1);
        store.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureEnrolledAsync_does_not_throw_on_a_non_transient_renewal_failure()
    {
        // Renewal window (valid but not current): even a non-transient failure (e.g. the renewal token
        // was already used) must NOT throw — the component keeps running on its current certificate. It
        // is logged at Error, but a single non-blocking attempt is made and no retry loop runs.
        var transport = new FakeTransport(
            failuresBeforeSuccess: int.MaxValue,
            failureFactory: () => new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized));
        var store = new FakeStore { Valid = true, Current = false };

        await Create(transport, store).EnsureEnrolledAsync(CancellationToken.None);

        transport.Calls.Should().Be(1);
        store.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureEnrolledAsync_fails_fast_on_a_tls_trust_failure_instead_of_retrying_forever()
    {
        // A wrong pinned CA / host-name mismatch surfaces as an HttpRequestException wrapping an
        // AuthenticationException. It is a misconfiguration retrying cannot fix, so blocking startup must
        // abort rather than loop forever.
        var transport = new FakeTransport(
            failuresBeforeSuccess: int.MaxValue,
            failureFactory: () => new HttpRequestException(
                "TLS failure", new AuthenticationException("the remote certificate is invalid")));
        var store = new FakeStore();

        var act = () => Create(transport, store).EnsureEnrolledAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        transport.Calls.Should().Be(1);
        store.SaveCount.Should().Be(0);
    }

    private sealed class FakeTransport(int failuresBeforeSuccess, Func<Exception>? failureFactory = null)
        : IEnrollmentTransport
    {
        public int Calls { get; private set; }

        public Task<ComponentEnrollmentResult> EnrollAsync(
            ComponentEnrollmentRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls <= failuresBeforeSuccess)
                throw failureFactory?.Invoke()
                    ?? new InvalidOperationException("the identity service is not ready");

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

        // Independently settable so the renewal window (valid but not current) can be modelled; a current
        // certificate is by definition also valid.
        public bool Valid { get; init; }
        public int SaveCount { get; private set; }

        public bool HasCurrentCertificate() => Current;
        public bool HasValidCertificate() => Current || Valid;
        public void Save(byte[] clientPkcs8PrivateKey, byte[] serverPkcs8PrivateKey, ComponentEnrollmentResult result) => SaveCount++;

        public System.Security.Cryptography.X509Certificates.X509Certificate2? LoadClientCertificate() => null;

        public string? GetClientCertificatePfxPath() => Current ? "fake.pfx" : null;

        public System.Security.Cryptography.X509Certificates.X509Certificate2Collection LoadCaTrustBundle() => [];

        public (System.Security.Cryptography.X509Certificates.X509Certificate2 Leaf,
            System.Security.Cryptography.X509Certificates.X509Certificate2Collection Chain) LoadServerCertificate() =>
            throw new System.NotSupportedException("The enrollment client tests do not use the server certificate.");

        public ComponentCertificatePem? ReadClientCertificatePem() => null;
    }
}
