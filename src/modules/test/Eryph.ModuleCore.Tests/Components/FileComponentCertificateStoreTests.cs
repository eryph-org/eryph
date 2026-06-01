using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.ModuleCore.Components;
using FluentAssertions;
using Xunit;

namespace Eryph.ModuleCore.Tests.Components;

public sealed class FileComponentCertificateStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "eryph-cert-store-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_then_load_reflects_validity_and_renewal_window()
    {
        using var key = RSA.Create(2048);
        // A certificate valid for ~90 days.
        var request = new CertificateRequest("CN=agent.eryph.local", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));

        var result = new ComponentEnrollmentResult
        {
            ComponentId = Guid.NewGuid(),
            Certificate = cert.Export(X509ContentType.Cert),
            IssuingChain = [cert.Export(X509ContentType.Cert)],
            CaTrustBundle = [cert.Export(X509ContentType.Cert)],
        };

        // Renewal lead time of 45 days: a 90-day cert is valid AND current.
        var store = new FileComponentCertificateStore(_dir, TimeSpan.FromDays(45));
        store.HasValidCertificate().Should().BeFalse("nothing is stored yet");

        store.Save(key.ExportPkcs8PrivateKey(), key.ExportPkcs8PrivateKey(), result);

        store.HasValidCertificate().Should().BeTrue();
        store.HasCurrentCertificate().Should().BeTrue();
        File.Exists(Path.Combine(_dir, "component.key")).Should().BeTrue();

        // A renewal lead time longer than the remaining lifetime makes it valid but not current.
        var renewalDue = new FileComponentCertificateStore(_dir, TimeSpan.FromDays(120));
        renewalDue.HasValidCertificate().Should().BeTrue();
        renewalDue.HasCurrentCertificate().Should().BeFalse("the cert is inside the renewal window");
    }

    [Fact]
    public void Is_not_valid_when_the_private_key_is_missing()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=agent.eryph.local", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));
        var result = new ComponentEnrollmentResult
        {
            ComponentId = Guid.NewGuid(),
            Certificate = cert.Export(X509ContentType.Cert),
            IssuingChain = [],
            CaTrustBundle = [cert.Export(X509ContentType.Cert)],
        };

        var store = new FileComponentCertificateStore(_dir, TimeSpan.FromDays(45));
        store.Save(key.ExportPkcs8PrivateKey(), key.ExportPkcs8PrivateKey(), result);
        store.HasValidCertificate().Should().BeTrue();

        // The key file is missing/corrupt (partial state): the leaf alone is not usable, so the store
        // must report not-valid/not-current and let startup re-enroll rather than fail later.
        File.Delete(Path.Combine(_dir, "component.key"));
        store.HasValidCertificate().Should().BeFalse();
        store.HasCurrentCertificate().Should().BeFalse();
    }

    [Fact]
    public void GetClientCertificatePfxPath_is_null_before_enrollment_and_loadable_after()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=agent.eryph.local", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));
        var result = new ComponentEnrollmentResult
        {
            ComponentId = Guid.NewGuid(),
            Certificate = cert.Export(X509ContentType.Cert),
            IssuingChain = [],
            CaTrustBundle = [cert.Export(X509ContentType.Cert)],
        };

        var store = new FileComponentCertificateStore(_dir, TimeSpan.FromDays(45));
        store.GetClientCertificatePfxPath().Should().BeNull("nothing is enrolled yet");

        store.Save(key.ExportPkcs8PrivateKey(), key.ExportPkcs8PrivateKey(), result);

        var pfxPath = store.GetClientCertificatePfxPath();
        pfxPath.Should().NotBeNull();
        File.Exists(pfxPath).Should().BeTrue();
        // The PFX loads and carries the private key (so the TLS stack can present it).
        using var loaded = X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(pfxPath!), password: null);
        loaded.HasPrivateKey.Should().BeTrue();
        loaded.Subject.Should().Contain("agent.eryph.local");

        // Re-creates the PFX if it is removed.
        File.Delete(pfxPath!);
        store.GetClientCertificatePfxPath().Should().NotBeNull();
        File.Exists(pfxPath).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
