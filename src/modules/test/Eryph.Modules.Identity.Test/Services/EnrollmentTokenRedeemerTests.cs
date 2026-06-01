#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.Messages.Components;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

public class EnrollmentTokenRedeemerTests
{
    private static ComponentCertificateAuthority CreateCa() =>
        new(new InMemoryCertificateStore(), new CertificateGenerator(), new InMemoryKeyService());

    // A shared in-memory database root makes separate contexts (modelling separate requests) see the
    // same redeemed-token rows, exactly as they would against a real database.
    private sealed class Store
    {
        private readonly InMemoryDatabaseRoot _root = new();
        private readonly string _name = "redeemer-" + Guid.NewGuid().ToString("N");

        public IdentityDbContext NewContext() =>
            new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(_name, _root).Options);
    }

    private static EnrollmentTokenRedeemer Redeemer(ComponentCertificateAuthority ca, IdentityDbContext context) =>
        new(ca, new IdentityDbRepository<RedeemedEnrollmentToken>(context));

    [Fact]
    public async Task RedeemAsync_valid_token_returns_the_bound_type()
    {
        var ca = CreateCa();
        var store = new Store();
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, DateTimeOffset.UtcNow.AddMinutes(5));

        var result = await Redeemer(ca, store.NewContext()).RedeemAsync(token, ComponentType.Controller);

        result.IsValid.Should().BeTrue();
        result.ComponentType.Should().Be(ComponentType.Controller);
    }

    [Fact]
    public async Task RedeemAsync_is_one_time()
    {
        var ca = CreateCa();
        var store = new Store();
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.ComputeApi, DateTimeOffset.UtcNow.AddMinutes(5));

        // Two separate contexts over the shared store model two requests.
        (await Redeemer(ca, store.NewContext()).RedeemAsync(token, ComponentType.ComputeApi)).IsValid.Should().BeTrue();
        (await Redeemer(ca, store.NewContext()).RedeemAsync(token, ComponentType.ComputeApi)).IsValid
            .Should().BeFalse("a one-time token cannot be redeemed twice");
    }

    [Fact]
    public async Task RedeemAsync_rejects_an_expired_token()
    {
        var ca = CreateCa();
        var store = new Store();
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, DateTimeOffset.UtcNow.AddSeconds(-5));

        (await Redeemer(ca, store.NewContext()).RedeemAsync(token, ComponentType.Controller)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RedeemAsync_does_not_consume_the_token_on_type_mismatch()
    {
        var ca = CreateCa();
        var store = new Store();
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, DateTimeOffset.UtcNow.AddMinutes(5));

        // A request for the wrong type is rejected WITHOUT burning the one-time token...
        (await Redeemer(ca, store.NewContext()).RedeemAsync(token, ComponentType.ComputeApi)).IsValid
            .Should().BeFalse("the token is bound to Controller, not ComputeApi");

        // ...so the legitimate component can still redeem it.
        (await Redeemer(ca, store.NewContext()).RedeemAsync(token, ComponentType.Controller)).IsValid
            .Should().BeTrue("a wrong-type attempt must not consume the token");
    }

    [Fact]
    public async Task RedeemAsync_rejects_a_tampered_or_foreign_or_malformed_token()
    {
        var ca = CreateCa();
        var store = new Store();
        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, DateTimeOffset.UtcNow.AddMinutes(5));
        var parts = token.Split('.');

        (await Redeemer(ca, store.NewContext()).RedeemAsync(parts[0] + "." + Flip(parts[1]), ComponentType.Controller))
            .IsValid.Should().BeFalse();
        var foreign = EnrollmentTokenCodec.Issue(CreateCa(), ComponentType.Controller, DateTimeOffset.UtcNow.AddMinutes(5));
        (await Redeemer(ca, store.NewContext()).RedeemAsync(foreign, ComponentType.Controller)).IsValid.Should().BeFalse();
        (await Redeemer(ca, store.NewContext()).RedeemAsync("a.b.c", ComponentType.Controller)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RedeemAsync_prunes_expired_redeemed_rows()
    {
        var ca = CreateCa();
        var store = new Store();

        // Seed an already-expired redeemed row.
        await using (var seed = store.NewContext())
        {
            seed.RedeemedEnrollmentTokens.Add(new RedeemedEnrollmentToken
            {
                Jti = "stale",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            });
            await seed.SaveChangesAsync();
        }

        var token = EnrollmentTokenCodec.Issue(ca, ComponentType.Controller, DateTimeOffset.UtcNow.AddMinutes(5));
        (await Redeemer(ca, store.NewContext()).RedeemAsync(token, ComponentType.Controller)).IsValid.Should().BeTrue();

        await using var check = store.NewContext();
        check.RedeemedEnrollmentTokens.Any(t => t.Jti == "stale").Should().BeFalse("expired rows are pruned");
        check.RedeemedEnrollmentTokens.Any(t => t.Jti != "stale").Should().BeTrue("the fresh token was recorded");
    }

    private static string Flip(string segment) =>
        (segment[0] == 'A' ? 'B' : 'A') + segment[1..];
}
