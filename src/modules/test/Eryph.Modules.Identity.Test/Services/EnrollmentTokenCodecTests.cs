#nullable enable
using System;
using Eryph.Messages.Components;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

public class EnrollmentTokenCodecTests
{
    private static ComponentCertificateAuthority CreateCa() =>
        new(new InMemoryCertificateStore(), new CertificateGenerator(), new InMemoryKeyService());

    [Fact]
    public void Issue_then_TryRead_returns_the_bound_type_and_expiry()
    {
        var ca = CreateCa();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, expiresAt);

        var content = EnrollmentTokenCodec.TryRead(ca, token);

        content.Should().NotBeNull();
        content!.ComponentType.Should().Be(ComponentType.Controller);
        content.Jti.Should().NotBeNullOrWhiteSpace();
        // The token carries a second-resolution unix expiry.
        content.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TryRead_does_not_enforce_expiry_that_is_the_redeemers_job()
    {
        var ca = CreateCa();
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, DateTimeOffset.UtcNow.AddSeconds(-5));

        var content = EnrollmentTokenCodec.TryRead(ca, token);

        content.Should().NotBeNull("the codec verifies the signature only; expiry is enforced by the redeemer");
        content!.ExpiresAt.Should().BeBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void TryRead_rejects_a_tampered_payload()
    {
        var ca = CreateCa();
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, DateTimeOffset.UtcNow.AddMinutes(5));
        var parts = token.Split('.');

        EnrollmentTokenCodec.TryRead(ca, Flip(parts[0]) + "." + parts[1]).Should().BeNull();
    }

    [Fact]
    public void TryRead_rejects_a_tampered_signature()
    {
        var ca = CreateCa();
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, DateTimeOffset.UtcNow.AddMinutes(5));
        var parts = token.Split('.');

        EnrollmentTokenCodec.TryRead(ca, parts[0] + "." + Flip(parts[1])).Should().BeNull();
    }

    [Fact]
    public void TryRead_rejects_a_token_from_a_different_ca()
    {
        var token = EnrollmentTokenCodec.Issue(CreateCa(), ComponentType.Controller, DateTimeOffset.UtcNow.AddMinutes(5));

        // A different identity (different CA root) must not accept the token.
        EnrollmentTokenCodec.TryRead(CreateCa(), token).Should().BeNull();
    }

    [Fact]
    public void TryRead_rejects_malformed_input()
    {
        var ca = CreateCa();
        EnrollmentTokenCodec.TryRead(ca, "").Should().BeNull();
        EnrollmentTokenCodec.TryRead(ca, "not-a-token").Should().BeNull();
        EnrollmentTokenCodec.TryRead(ca, "a.b.c").Should().BeNull();
    }

    private static string Flip(string segment) =>
        (segment[0] == 'A' ? 'B' : 'A') + segment[1..];
}
