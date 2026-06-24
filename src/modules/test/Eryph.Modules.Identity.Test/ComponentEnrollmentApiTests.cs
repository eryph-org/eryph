#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using Eryph.Messages.Components;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Endpoints.V1.Components;
using Eryph.Modules.Identity.Models;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Identity.Test;

public class ComponentEnrollmentApiTests
{
    private static string NewPublicKey()
    {
        using var key = RSA.Create(2048);
        return Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    private static EnrollComponentRequest Valid() =>
        new()
        {
            ComponentType = ComponentType.Controller,
            Fqdn = "controller1.eryph.local",
            PublicKey = NewPublicKey(),
            Token = "enroll-token",
        };

    [Fact]
    public void Validate_accepts_a_well_formed_request()
    {
        ComponentEnrollmentValidations.Validate(Valid()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateForRenewal_does_not_require_a_token_but_still_validates_the_keys()
    {
        // Renewal authenticates with the certificate, not a token, so a renewal request with no token is
        // valid — but the public key is still validated.
        var request = Valid();
        request.Token = "";

        ComponentEnrollmentValidations.ValidateForRenewal(request).IsSuccess.Should().BeTrue();

        request.PublicKey = "not-base64!!";
        ComponentEnrollmentValidations.ValidateForRenewal(request).IsFail.Should().BeTrue();
    }

    [Fact]
    public void ValidateForRenewal_enforces_the_server_dns_name_count_cap()
    {
        // The renew endpoint is anonymous (cert-authenticated), so it must apply the same count cap as
        // enroll to bound the per-request work a component can force.
        var request = Valid();
        request.Token = "";
        request.ServerPublicKey = NewPublicKey();
        request.ServerDnsNames = Enumerable.Range(0, 17).Select(i => $"name{i}.eryph.local").ToList();

        var validation = ComponentEnrollmentValidations.ValidateForRenewal(request);

        validation.IsFail.Should().BeTrue();
        validation.ToModelStateDictionary().Keys.Should().Contain("$.server_dns_names");
    }

    [Fact]
    public void Validate_accepts_a_server_certificate_request_with_valid_names()
    {
        var request = Valid();
        request.ServerPublicKey = NewPublicKey();
        request.ServerDnsNames = ["controller1.eryph.local", "controller1"];

        ComponentEnrollmentValidations.Validate(request).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("token", "$.token")]
    [InlineData("fqdn", "$.fqdn")]
    [InlineData("public_key", "$.public_key")]
    public void Validate_reports_the_offending_member(string field, string expectedMember)
    {
        var request = Valid();
        switch (field)
        {
            case "token": request.Token = ""; break;
            case "fqdn": request.Fqdn = "not a dns name"; break;
            case "public_key": request.PublicKey = "not-base64!!"; break;
        }

        var validation = ComponentEnrollmentValidations.Validate(request);

        validation.IsFail.Should().BeTrue();
        validation.ToModelStateDictionary().Keys.Should().Contain(expectedMember);
    }

    [Fact]
    public void Validate_rejects_an_invalid_server_dns_name()
    {
        var request = Valid();
        request.ServerPublicKey = NewPublicKey();
        request.ServerDnsNames = ["*.eryph.local"];

        var validation = ComponentEnrollmentValidations.Validate(request);

        validation.IsFail.Should().BeTrue();
        validation.ToModelStateDictionary().Keys.Should().Contain("$.server_dns_names[0]");
    }

    [Fact]
    public void Validate_rejects_a_null_server_dns_name_without_throwing()
    {
        // A JSON body of {"server_dns_names":[null]} deserializes to a null entry; validation must
        // report it as a 400 issue, never throw (which would surface as a 500 on the anonymous endpoint).
        var request = Valid();
        request.ServerPublicKey = NewPublicKey();
        request.ServerDnsNames = [null!];

        var validation = ComponentEnrollmentValidations.Validate(request);

        validation.IsFail.Should().BeTrue();
        validation.ToModelStateDictionary().Keys.Should().Contain("$.server_dns_names[0]");
    }

    [Fact]
    public void Validate_rejects_too_many_server_dns_names()
    {
        var request = Valid();
        request.ServerPublicKey = NewPublicKey();
        request.ServerDnsNames = Enumerable.Range(0, 100).Select(i => $"name{i}.eryph.local").ToList();

        var validation = ComponentEnrollmentValidations.Validate(request);

        validation.IsFail.Should().BeTrue();
        validation.ToModelStateDictionary().Keys.Should().Contain("$.server_dns_names");
    }

    [Fact]
    public void Validate_rejects_server_dns_names_without_a_server_public_key()
    {
        var request = Valid();
        request.ServerDnsNames = ["api.eryph.local"]; // no ServerPublicKey

        var validation = ComponentEnrollmentValidations.Validate(request);

        validation.IsFail.Should().BeTrue();
        validation.ToModelStateDictionary().Keys.Should().Contain("$.server_dns_names");
    }

    [Fact]
    public void Validate_rejects_an_undefined_component_type()
    {
        var request = Valid();
        request.ComponentType = (ComponentType)999;

        var validation = ComponentEnrollmentValidations.Validate(request);

        validation.IsFail.Should().BeTrue();
        validation.ToModelStateDictionary().Keys.Should().Contain("$.component_type");
    }

    [Fact]
    public void Mapping_round_trips_between_api_model_and_service_contract()
    {
        var request = Valid();
        request.ServerPublicKey = NewPublicKey();
        request.ServerDnsNames = ["controller1.eryph.local"];

        var serviceRequest = request.ToServiceRequest();
        serviceRequest.ComponentType.Should().Be(ComponentType.Controller);
        serviceRequest.Fqdn.Should().Be("controller1.eryph.local");
        serviceRequest.PublicKey.Should().Equal(Convert.FromBase64String(request.PublicKey));
        serviceRequest.Token.Should().Be("enroll-token");

        var result = new ComponentEnrollmentResultBuilder().Build();
        var api = result.ToApiModel();
        api.ComponentId.Should().Be(result.ComponentId.ToString());
        api.Certificate.Should().Be(Convert.ToBase64String(result.Certificate));
        api.ServerCertificate.Should().BeEmpty("no server certificate was issued");
        api.CaTrustBundle.Should().ContainSingle();
    }

    // Local helper to build a service result without depending on the CA.
    private sealed class ComponentEnrollmentResultBuilder
    {
        public ModuleCore.Components.ComponentEnrollmentResult Build() => new()
        {
            ComponentId = Guid.NewGuid(),
            Certificate = [1, 2, 3],
            IssuingChain = [new byte[] { 4, 5 }],
            CaTrustBundle = [new byte[] { 6, 7 }],
        };
    }
}
