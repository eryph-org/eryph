#nullable enable
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Identity.Test.Services;

public class CaServerCertificateProviderTests
{
    [Fact]
    public void GetServerCertificate_returns_a_server_leaf_with_key_chaining_to_the_ca()
    {
        var keyService = new InMemoryKeyService();
        var ca = new ComponentCertificateAuthority(
            new InMemoryCertificateStore(), new CertificateGenerator(), keyService);
        var sut = new CaServerCertificateProvider(keyService, ca);

        var issued = sut.GetServerCertificate("identity.eryph.local");

        issued.Leaf.HasPrivateKey.Should().BeTrue("the listener must be able to present the key");
        issued.IssuingChain.Should().NotBeEmpty();

        issued.Leaf.Extensions.OfType<X509EnhancedKeyUsageExtension>()
            .Single().EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>()
            .Select(o => o.Value).Should().Contain("1.3.6.1.5.5.7.3.1"); // serverAuth

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        foreach (var root in ca.GetTrustedCaCertificates())
            chain.ChainPolicy.CustomTrustStore.Add(root);
        foreach (var intermediate in issued.IssuingChain)
            chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(issued.Leaf).Should().BeTrue("the server certificate must chain to the component root");
    }
}
