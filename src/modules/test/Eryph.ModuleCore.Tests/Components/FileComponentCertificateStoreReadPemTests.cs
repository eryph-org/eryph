using System;
using System.IO;
using FluentAssertions;
using Eryph.ModuleCore.Components;
using Xunit;

namespace Eryph.ModuleCore.Tests.Components;

public class FileComponentCertificateStoreReadPemTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "eryph-certstore-test-" + Guid.NewGuid().ToString("N"));

    public FileComponentCertificateStoreReadPemTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private FileComponentCertificateStore Store() => new(_dir, TimeSpan.FromDays(45));

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    [Fact]
    public void ReadClientCertificatePem_AllFilesPresent_ReturnsPem()
    {
        Write("component.key", "KEYPEM");
        Write("component.crt", "CERTPEM");
        Write("ca-bundle.pem", "CAPEM");

        var pem = Store().ReadClientCertificatePem();

        pem.Should().NotBeNull();
        pem!.PrivateKeyPem.Should().Be("KEYPEM");
        pem.CertificatePem.Should().Be("CERTPEM");
        pem.CaBundlePem.Should().Be("CAPEM");
    }

    [Fact]
    public void ReadClientCertificatePem_AppendsIssuingChainToCertificate()
    {
        Write("component.key", "KEYPEM");
        Write("component.crt", "LEAF\n");
        Write("issuing-chain.pem", "INTERMEDIATE\n");
        Write("ca-bundle.pem", "CAPEM");

        var pem = Store().ReadClientCertificatePem();

        pem!.CertificatePem.Should().Be("LEAF\nINTERMEDIATE\n");
    }

    [Theory]
    [InlineData("component.key")]
    [InlineData("component.crt")]
    [InlineData("ca-bundle.pem")]
    public void ReadClientCertificatePem_MissingRequiredFile_ReturnsNull(string missing)
    {
        foreach (var name in new[] { "component.key", "component.crt", "ca-bundle.pem" })
            if (name != missing)
                Write(name, "X");

        Store().ReadClientCertificatePem().Should().BeNull();
    }
}
