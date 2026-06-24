#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

/// <summary>
/// Verifies CA rotation: a new root/intermediate generation takes over signing while the previous
/// generation stays trusted for the overlap, and retirement drops the old generation afterwards.
/// </summary>
public class ComponentCertificateAuthorityRotationTests
{
    private static ComponentCertificateAuthority CreateCa() =>
        new(new InMemoryCertificateStore(), new CertificateGenerator(), new InMemoryKeyService());

    private static X509Certificate2 IssueClientLeaf(ComponentCertificateAuthority ca, string fqdn)
    {
        using var key = RSA.Create(2048);
        using var issued = ca.IssueComponentCertificate(
            ComponentIdentity.DeriveComponentId(ComponentType.VMHostAgent, fqdn).ToString(), fqdn, key);
        return X509CertificateLoader.LoadCertificate(issued.Leaf.RawData);
    }

    // Issues a server certificate so the server-TLS intermediate exists in the store (it is minted
    // lazily on first server-cert issuance, just like the client intermediate).
    private static void IssueServerLeaf(ComponentCertificateAuthority ca, string dnsName)
    {
        using var key = RSA.Create(2048);
        using var issued = ca.IssueServerCertificate([dnsName], key);
    }

    // Builds leaf -> intermediate -> root against the CA's own trust bundle + issuing intermediates,
    // exactly as a relying party would. Disposes the borrowed CA certificate handles.
    private static bool ChainsToTrust(ComponentCertificateAuthority ca, X509Certificate2 leaf)
    {
        var roots = ca.GetTrustedCaCertificates();
        var intermediates = ca.GetIssuingCaCertificates();
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            foreach (var root in roots)
                chain.ChainPolicy.CustomTrustStore.Add(root);
            foreach (var intermediate in intermediates)
                chain.ChainPolicy.ExtraStore.Add(intermediate);
            return chain.Build(leaf);
        }
        finally
        {
            foreach (var certificate in roots) certificate.Dispose();
            foreach (var certificate in intermediates) certificate.Dispose();
        }
    }

    private static IReadOnlyList<X509Certificate2> Roots(ComponentCertificateAuthority ca)
    {
        var roots = ca.GetTrustedCaCertificates();
        foreach (var root in roots) root.Dispose();
        return roots;
    }

    [Fact]
    public void Rotation_keeps_both_roots_trusted_and_signs_new_leaves_with_the_new_root()
    {
        var ca = CreateCa();
        using var leafBefore = IssueClientLeaf(ca, "agent1.eryph.local");
        Roots(ca).Should().ContainSingle("only the initial root exists before rotation");

        ca.RotateRootCertificateAuthority();

        Roots(ca).Should().HaveCount(2, "the previous root stays trusted during the overlap");
        using var leafAfter = IssueClientLeaf(ca, "agent1.eryph.local");

        // Distinct certificates issued across the rotation (sanity check the second issue happened).
        leafAfter.SerialNumber.Should().NotBe(leafBefore.SerialNumber);

        // Both validate against the overlapping trust bundle (old and new generations both present).
        ChainsToTrust(ca, leafBefore).Should().BeTrue("the pre-rotation leaf still chains during overlap");
        ChainsToTrust(ca, leafAfter).Should().BeTrue("the post-rotation leaf chains to the new root");
    }

    [Fact]
    public void Issuing_intermediates_include_both_generations_during_the_overlap()
    {
        var ca = CreateCa();
        // Issue both a client and a server leaf so both tier intermediates exist (they are minted
        // lazily on first issuance), establishing one generation of each before rotation.
        using var _ = IssueClientLeaf(ca, "agent1.eryph.local");
        IssueServerLeaf(ca, "agent1.eryph.local");

        var before = ca.GetIssuingCaCertificates();
        var beforeCount = before.Count;
        foreach (var c in before) c.Dispose();

        ca.RotateRootCertificateAuthority();

        var after = ca.GetIssuingCaCertificates();
        try
        {
            // Two tiers (client + server) before; both generations of each after.
            beforeCount.Should().Be(2);
            after.Should().HaveCount(4, "both the superseded and the new client+server intermediates are valid");
        }
        finally
        {
            foreach (var c in after) c.Dispose();
        }
    }

    [Fact]
    public void Retirement_drops_the_old_generation_so_old_leaves_no_longer_validate()
    {
        var ca = CreateCa();
        using var oldLeaf = IssueClientLeaf(ca, "agent1.eryph.local");
        ca.RotateRootCertificateAuthority();
        using var newLeaf = IssueClientLeaf(ca, "agent1.eryph.local");

        ca.RetireSupersededCertificateAuthorities();

        Roots(ca).Should().ContainSingle("only the current signing root remains after retirement");
        ChainsToTrust(ca, newLeaf).Should().BeTrue("the current-generation leaf still validates");
        ChainsToTrust(ca, oldLeaf).Should().BeFalse(
            "the superseded generation was removed, so a leaf issued under it no longer chains");
    }

    [Fact]
    public void Retirement_without_a_prior_rotation_is_a_noop()
    {
        var ca = CreateCa();
        using var leaf = IssueClientLeaf(ca, "agent1.eryph.local");

        ca.RetireSupersededCertificateAuthorities();

        Roots(ca).Should().ContainSingle();
        ChainsToTrust(ca, leaf).Should().BeTrue("the only generation must remain trusted");
    }

    [Fact]
    public void Rotation_does_not_collide_on_a_backend_that_refuses_to_overwrite_keys()
    {
        // Mimics the Windows CNG backend, where GeneratePersistedRsaKey throws if the key name already
        // exists. Rotation reuses the tier key names, so it must delete the demoted generation's keys
        // before regenerating. The in-memory/file backends silently overwrite and so cannot catch this.
        var ca = new ComponentCertificateAuthority(
            new InMemoryCertificateStore(), new CertificateGenerator(), new NonOverwritingKeyService());
        using var _ = IssueClientLeaf(ca, "agent1.eryph.local");
        IssueServerLeaf(ca, "agent1.eryph.local");

        var act = () => ca.RotateRootCertificateAuthority();

        act.Should().NotThrow();
        Roots(ca).Should().HaveCount(2, "both generations are trusted after a successful rotation");
    }

    // A key service that refuses to overwrite an existing persisted key name, like Windows CNG
    // (CngKey.Create without OverwriteExistingKey throws "the key already exists"). The production
    // InMemory/File services silently overwrite, which is why they cannot guard rotation's key reuse.
    private sealed class NonOverwritingKeyService : ICertificateKeyService
    {
        private readonly Dictionary<string, RSA> _keys = new();

        public RSA GenerateRsaKey(int keyLength) => RSA.Create(keyLength);

        public RSA GeneratePersistedRsaKey(string keyName, int keyLength)
        {
            if (_keys.ContainsKey(keyName))
                throw new CryptographicException($"The key '{keyName}' already exists.");
            var key = RSA.Create(keyLength);
            _keys[keyName] = key;
            return Clone(key);
        }

        public RSA? GetPersistedRsaKey(string keyName) =>
            _keys.TryGetValue(keyName, out var stored) ? Clone(stored) : null;

        public void DeletePersistedKey(string keyName)
        {
            if (_keys.Remove(keyName, out var key))
                key.Dispose();
        }

        private static RSA Clone(RSA key)
        {
            var copy = RSA.Create();
            copy.ImportRSAPrivateKey(key.ExportRSAPrivateKey(), out _);
            return copy;
        }
    }
}
