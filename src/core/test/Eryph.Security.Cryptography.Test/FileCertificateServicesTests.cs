using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Xunit;

namespace Eryph.Security.Cryptography.Test;

public sealed class FileCertificateServicesTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "eryph-pki-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static X509Certificate2 SelfSignedWithKey(string subject)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
    }

    // --- key service ---

    [Fact]
    public void PersistedKey_round_trips_and_deletes()
    {
        var sut = new FileCertificateKeyService(_dir);

        using (var created = sut.GeneratePersistedRsaKey("eryph-test-key", 2048))
        using (var loaded = sut.GetPersistedRsaKey("eryph-test-key"))
        {
            loaded.Should().NotBeNull();
            loaded!.ExportSubjectPublicKeyInfo().Should().Equal(created.ExportSubjectPublicKeyInfo());
        }

        sut.DeletePersistedKey("eryph-test-key");
        sut.GetPersistedRsaKey("eryph-test-key").Should().BeNull();
    }

    [Fact]
    public void GetPersistedKey_is_null_when_absent()
    {
        new FileCertificateKeyService(_dir).GetPersistedRsaKey("missing").Should().BeNull();
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("")]
    public void Key_methods_reject_unsafe_names(string keyName)
    {
        var sut = new FileCertificateKeyService(_dir);
        ((Action)(() => sut.GeneratePersistedRsaKey(keyName, 2048))).Should().Throw<ArgumentException>();
        ((Action)(() => sut.GetPersistedRsaKey(keyName))).Should().Throw<ArgumentException>();
        ((Action)(() => sut.DeletePersistedKey(keyName))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PersistedKey_file_is_owner_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
            return; // NTFS ACL inheritance on Windows; nothing to assert here.

        using var _ = new FileCertificateKeyService(_dir).GeneratePersistedRsaKey("perm-key", 2048);
        var mode = File.GetUnixFileMode(Path.Combine(_dir, "perm-key.key"));
        mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    // --- store service ---

    [Fact]
    public void MyStore_returns_the_certificate_with_its_private_key()
    {
        var sut = new FileCertificateStoreService(_dir);
        using var cert = SelfSignedWithKey("CN=eryph-store-test");

        sut.AddToMyStore(cert);
        var loaded = sut.GetFromMyStore(cert.SubjectName);

        loaded.Should().ContainSingle();
        loaded[0].HasPrivateKey.Should().BeTrue("the CA and token manager sign with the key returned from the store");
        // The returned key must actually sign.
        using var key = loaded[0].GetRSAPrivateKey();
        key!.SignData([1, 2, 3], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Should().NotBeEmpty();
    }

    [Fact]
    public void MyStore_pfx_is_owner_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
            return; // NTFS ACL inheritance on Windows.

        var sut = new FileCertificateStoreService(_dir);
        using var cert = SelfSignedWithKey("CN=eryph-store-perm");
        sut.AddToMyStore(cert);

        var pfx = Directory.EnumerateFiles(Path.Combine(_dir, "my"), "*.pfx").Single();
        File.GetUnixFileMode(pfx).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite,
            "the PKCS#12 holds a private key and must not be group/world readable");
    }

    [Fact]
    public void MyStore_filters_by_subject_and_keeps_multiple_generations()
    {
        var sut = new FileCertificateStoreService(_dir);
        using var a1 = SelfSignedWithKey("CN=eryph-ca");
        using var a2 = SelfSignedWithKey("CN=eryph-ca");      // same subject, new generation
        using var b = SelfSignedWithKey("CN=other");
        sut.AddToMyStore(a1);
        sut.AddToMyStore(a2);
        sut.AddToMyStore(b);

        sut.GetFromMyStore(a1.SubjectName).Should().HaveCount(2);
        sut.GetFromMyStore(b.SubjectName).Should().ContainSingle();
    }

    [Fact]
    public void RemoveFromMyStore_by_subject_and_by_public_key()
    {
        var sut = new FileCertificateStoreService(_dir);
        using var cert = SelfSignedWithKey("CN=eryph-remove");
        using var other = SelfSignedWithKey("CN=eryph-keep");
        sut.AddToMyStore(cert);
        sut.AddToMyStore(other);

        sut.RemoveFromMyStore(cert.SubjectName);
        sut.GetFromMyStore(cert.SubjectName).Should().BeEmpty();
        sut.GetFromMyStore(other.SubjectName).Should().ContainSingle();

        sut.RemoveFromMyStore(other.PublicKey);
        sut.GetFromMyStore(other.SubjectName).Should().BeEmpty();
    }
}
