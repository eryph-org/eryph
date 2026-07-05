using Eryph.ModuleCore.Components;
using FluentAssertions;

namespace Eryph.ModuleCore.Tests.Components;

public class FileComponentCertificateStoreReadPemTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "eryph-certstore-test-" + Guid.NewGuid().ToString("N"));

    public FileComponentCertificateStoreReadPemTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
            /* best effort */
        }
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

    [Fact]
    public void ReadServerCertificatePem_AllFilesPresent_ReturnsPem()
    {
        Write("server.key", "KEYPEM");
        Write("server.crt", "CERTPEM");
        Write("ca-bundle.pem", "CAPEM");

        var pem = Store().ReadServerCertificatePem();

        pem.Should().NotBeNull();
        pem!.PrivateKeyPem.Should().Be("KEYPEM");
        pem.CertificatePem.Should().Be("CERTPEM");
        pem.CaBundlePem.Should().Be("CAPEM");
    }

    [Fact]
    public void ReadServerCertificatePem_AppendsIssuingChainToCertificate()
    {
        Write("server.key", "KEYPEM");
        Write("server.crt", "LEAF\n");
        Write("server-chain.pem", "INTERMEDIATE\n");
        Write("ca-bundle.pem", "CAPEM");

        var pem = Store().ReadServerCertificatePem();

        pem!.CertificatePem.Should().Be("LEAF\nINTERMEDIATE\n");
    }

    [Fact]
    public void ReadServerCertificatePem_ReadsServerTripleNotClientTriple()
    {
        // The server method must read the server.* files, not the client component.* files: the two
        // present different EKUs (serverAuth vs clientAuth), so mixing them would break TLS.
        Write("component.key", "CLIENTKEY");
        Write("component.crt", "CLIENTCERT");
        Write("server.key", "SERVERKEY");
        Write("server.crt", "SERVERCERT");
        Write("ca-bundle.pem", "CAPEM");

        var pem = Store().ReadServerCertificatePem();

        pem!.PrivateKeyPem.Should().Be("SERVERKEY");
        pem.CertificatePem.Should().Be("SERVERCERT");
    }

    [Theory]
    [InlineData("server.key")]
    [InlineData("server.crt")]
    [InlineData("ca-bundle.pem")]
    public void ReadServerCertificatePem_MissingRequiredFile_ReturnsNull(string missing)
    {
        foreach (var name in new[] { "server.key", "server.crt", "ca-bundle.pem" })
            if (name != missing)
                Write(name, "X");

        Store().ReadServerCertificatePem().Should().BeNull();
    }
}
