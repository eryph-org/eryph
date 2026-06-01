#nullable enable
using System;
using Eryph.Messages.Components;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

public class EnrollmentTokenServiceTests
{
    private static EnrollmentTokenService Create() =>
        new(new ComponentCertificateAuthority(new InMemoryCertificateStore(), new CertificateGenerator(), new InMemoryKeyService()),
            new RedeemedTokenStore());

    [Fact]
    public void Mint_then_Redeem_is_valid_and_returns_the_bound_type()
    {
        var service = Create();
        var token = service.Mint(ComponentType.Controller, TimeSpan.FromMinutes(5));

        var result = service.Redeem(token);

        result.IsValid.Should().BeTrue();
        result.ComponentType.Should().Be(ComponentType.Controller);
    }

    [Fact]
    public void Redeem_is_one_time()
    {
        var service = Create();
        var token = service.Mint(ComponentType.ComputeApi, TimeSpan.FromMinutes(5));

        service.Redeem(token).IsValid.Should().BeTrue();
        service.Redeem(token).IsValid.Should().BeFalse("a one-time token cannot be redeemed twice");
    }

    [Fact]
    public void Redeem_rejects_an_expired_token()
    {
        var service = Create();
        var token = service.Mint(ComponentType.Controller, TimeSpan.FromSeconds(-5));

        service.Redeem(token).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Redeem_rejects_a_tampered_payload()
    {
        var service = Create();
        var token = service.Mint(ComponentType.Controller, TimeSpan.FromMinutes(5));
        var parts = token.Split('.');
        var tampered = Flip(parts[0]) + "." + parts[1];

        service.Redeem(tampered).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Redeem_rejects_a_tampered_signature()
    {
        var service = Create();
        var token = service.Mint(ComponentType.Controller, TimeSpan.FromMinutes(5));
        var parts = token.Split('.');
        var tampered = parts[0] + "." + Flip(parts[1]);

        service.Redeem(tampered).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Redeem_rejects_a_token_minted_by_a_different_ca()
    {
        var token = Create().Mint(ComponentType.Controller, TimeSpan.FromMinutes(5));

        // A different identity (different CA root) must not accept the token.
        Create().Redeem(token).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Redeem_rejects_malformed_input()
    {
        var service = Create();
        service.Redeem("").IsValid.Should().BeFalse();
        service.Redeem("not-a-token").IsValid.Should().BeFalse();
        service.Redeem("a.b.c").IsValid.Should().BeFalse();
    }

    private static string Flip(string segment) =>
        (segment[0] == 'A' ? 'B' : 'A') + segment[1..];
}
