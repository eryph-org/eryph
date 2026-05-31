#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

public class ComponentCertificateAuthorityTests
{
    private static ComponentCertificateAuthority CreateCa() =>
        new(new InMemoryCertificateStore(), new CertificateGenerator(), new InMemoryKeyService());

    [Fact]
    public void GetCaCertificate_creates_a_usable_ca()
    {
        var ca = CreateCa().GetCaCertificate();

        ca.HasPrivateKey.Should().BeTrue();
        ca.Extensions.OfType<X509BasicConstraintsExtension>()
            .Should().ContainSingle().Which.CertificateAuthority.Should().BeTrue();
        ca.Extensions.OfType<X509KeyUsageExtension>()
            .Should().ContainSingle().Which.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.KeyCertSign);
    }

    [Fact]
    public void GetCaCertificate_returns_the_same_certificate_on_repeated_calls()
    {
        var sut = CreateCa();

        var first = sut.GetCaCertificate();
        var second = sut.GetCaCertificate();

        // Reused from the store rather than regenerated (same key, same serial).
        second.Thumbprint.Should().Be(first.Thumbprint);
    }

    [Fact]
    public void IssueComponentCertificate_issues_a_leaf_that_chains_to_the_ca()
    {
        var sut = CreateCa();
        var ca = sut.GetCaCertificate();

        using var componentKey = RSA.Create(2048);
        var componentId = "11111111-2222-3333-4444-555555555555";
        const string fqdn = "agent1.eryph.local";

        var issued = sut.IssueComponentCertificate(componentId, fqdn, componentKey);

        // Signed by the CA.
        issued.IssuerName.RawData.Should().Equal(ca.SubjectName.RawData);

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(issued).Should().BeTrue("the issued certificate must chain to the component CA");

        // Authenticates as a TLS client.
        issued.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .Single().EnhancedKeyUsages.Cast<Oid>()
            .Select(o => o.Value).Should().Contain("1.3.6.1.5.5.7.3.2");

        // Carries the FQDN and the stable component id as subject alternative names.
        var san = issued.Extensions.Single(e => e.Oid?.Value == "2.5.29.17").Format(false);
        san.Should().Contain(fqdn);
        san.Should().Contain(componentId);

        // Short-lived (~90 days) so renewal stays a routine, exercised path.
        (issued.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays.Should().BeInRange(85, 95);
    }

    [Fact]
    public void GetTrustedCaCertificates_returns_the_active_ca()
    {
        var sut = CreateCa();
        var ca = sut.GetCaCertificate();

        var bundle = sut.GetTrustedCaCertificates();

        bundle.Should().ContainSingle().Which.Thumbprint.Should().Be(ca.Thumbprint);
    }

    [Fact]
    public void GetCaCertificate_does_not_remove_other_valid_ca_generations()
    {
        var store = new InMemoryCertificateStore();
        var sut = new ComponentCertificateAuthority(store, new CertificateGenerator(), new InMemoryKeyService());

        // First generation (managed by the service).
        sut.GetCaCertificate();

        // Simulate a second, still-valid CA generation under the same subject (as a future
        // rollover would add). The service must keep both as trust anchors, not wipe one.
        var subject = new X500DistinguishedNameBuilder();
        subject.AddOrganizationName("eryph");
        subject.AddOrganizationalUnitName("component-ca");
        subject.AddCommonName("eryph-component-ca");
        using var secondKey = RSA.Create(2048);
        var secondGeneration = new CertificateGenerator().GenerateCaCertificate(
            subject.Build(), "eryph component CA", secondKey, 3650, []);
        store.AddToMyStore(secondGeneration);

        sut.GetCaCertificate();

        sut.GetTrustedCaCertificates().Should().HaveCount(2);
    }

    private sealed class InMemoryCertificateStore : ICertificateStoreService
    {
        private readonly List<X509Certificate2> _certificates = [];

        public void AddToMyStore(X509Certificate2 certificate) => _certificates.Add(certificate);

        public IReadOnlyList<X509Certificate2> GetFromMyStore(X500DistinguishedName subjectName) =>
            _certificates.Where(c => c.SubjectName.RawData.SequenceEqual(subjectName.RawData)).ToList();

        public void RemoveFromMyStore(X500DistinguishedName subjectName) =>
            _certificates.RemoveAll(c => c.SubjectName.RawData.SequenceEqual(subjectName.RawData));

        public void RemoveFromMyStore(PublicKey subjectKey) =>
            _certificates.RemoveAll(c => c.GetPublicKey().SequenceEqual(subjectKey.EncodedKeyValue.RawData));

        // The component CA only uses the "my" store; root-store operations are not exercised here.
        public void AddToRootStore(X509Certificate2 certificate) => throw new NotSupportedException();

        public IReadOnlyList<X509Certificate2> GetFromRootStore(X500DistinguishedName subjectName) => [];

        public void RemoveFromRootStore(X500DistinguishedName subjectName) => throw new NotSupportedException();

        public void RemoveFromRootStore(PublicKey subjectKey) => throw new NotSupportedException();
    }

    private sealed class InMemoryKeyService : ICertificateKeyService
    {
        private readonly Dictionary<string, RSA> _keys = [];

        public RSA GenerateRsaKey(int keyLength) => RSA.Create(keyLength);

        public RSA GeneratePersistedRsaKey(string keyName, int keyLength)
        {
            var key = RSA.Create(keyLength);
            _keys[keyName] = key;
            return key;
        }

        public RSA? GetPersistedRsaKey(string keyName) =>
            _keys.TryGetValue(keyName, out var key) ? key : null;

        public void DeletePersistedKey(string keyName) => _keys.Remove(keyName);
    }
}
