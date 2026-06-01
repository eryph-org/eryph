using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.Modules.Identity.Services;
using Eryph.Rebus;
using Eryph.Security.Cryptography;
using RabbitMQ.Client;

// Dev/operator provisioning harness — drives the IN-REPO component CA so a developer can stand up a
// local split-runtime mTLS cluster. In production this is the job of out-of-repo orchestration; this
// tool exists only to make the steps reproducible and documented. It must run elevated (the CA uses
// the LocalMachine certificate store and machine CNG keys, exactly like eryph-zero).
//
// Commands:
//   status                                  show CA certs in LocalMachine\My + whether the CNG keys persist
//   init                                    create/reuse the root + both intermediates (idempotent)
//   export-bundle <ca-bundle.pem>           write the trust bundle (root + intermediates, public) as PEM
//   issue-server <dns> <crt.pem> <key.pem>  issue a server cert (leaf+chain PEM) and its PKCS#8 key
//   provision-broker <dns> <outDir>         init + export-bundle + issue-server in one step

var keyService = new WindowsCertificateKeyService();
var generator = new WindowsCertificateGenerator();
var store = new WindowsCertificateStoreService();
var ca = new ComponentCertificateAuthority(store, generator, keyService);

// CA tier identities — must match ComponentCertificateAuthority's private CaTier definitions.
var tiers = new[]
{
    ("root", "eryph-component-root-ca", "eryph-component-root-ca-key"),
    ("client-ca", "eryph-component-client-ca", "eryph-component-client-ca-key"),
    ("server-ca", "eryph-server-tls-ca", "eryph-server-tls-ca-key"),
};

X500DistinguishedName CaSubject(string cn)
{
    var b = new X500DistinguishedNameBuilder();
    b.AddOrganizationName("eryph");
    b.AddOrganizationalUnitName("component-ca");
    b.AddCommonName(cn);
    return b.Build();
}

bool MachineKeyExists(string name) =>
    CngKey.Exists(name, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.MachineKey);

void Status()
{
    Console.WriteLine("CA material in Cert:\\LocalMachine\\My + machine CNG keys:");
    foreach (var (role, cn, keyName) in tiers)
    {
        var certs = store.GetFromMyStore(CaSubject(cn));
        var keyPersists = MachineKeyExists(keyName);
        if (certs.Count == 0)
        {
            Console.WriteLine($"  [{role,-9}] CN={cn}: NO CERT, keyPersists={keyPersists}");
            continue;
        }

        foreach (var c in certs)
            Console.WriteLine(
                $"  [{role,-9}] CN={cn}: thumb={c.Thumbprint} notAfter={c.NotAfter:u} hasPrivKey={c.HasPrivateKey} keyPersists={keyPersists}");
    }
}

void Init()
{
    // GetTrustedCaCertificates ensures the root; issuing one cert of each kind forces the
    // corresponding intermediate to be created (and persisted) on first use.
    var roots = ca.GetTrustedCaCertificates();
    using (var k = keyService.GenerateRsaKey(2048))
        _ = ca.IssueComponentCertificate("provision-warmup", "provision-warmup", k);
    using (var k = keyService.GenerateRsaKey(2048))
        _ = ca.IssueServerCertificate(["provision-warmup"], k);

    Console.WriteLine($"CA initialised. Roots: {roots.Count}.");
    Status();
}

IReadOnlyList<X509Certificate2> BundleCerts()
{
    var bundle = new List<X509Certificate2>();
    foreach (var (_, cn, _) in tiers)
        bundle.AddRange(store.GetFromMyStore(CaSubject(cn)));
    return bundle;
}

void ExportBundle(string path)
{
    var bundle = BundleCerts();
    if (bundle.Count == 0)
        throw new InvalidOperationException("No CA certificates found — run 'init' first.");

    using var w = new StreamWriter(path, append: false);
    foreach (var c in bundle)
        w.WriteLine(c.ExportCertificatePem());

    Console.WriteLine($"Wrote {bundle.Count} CA certificate(s) to {path}");
}

void IssueClient(string componentId, string fqdn, string crtPath, string keyPath)
{
    // Mirrors what a component receives from enrollment: the leaf only (the issuing chain is
    // delivered separately). Used to probe the broker's client-certificate validation.
    // The component generates the key and keeps it; the CA issues the leaf for the public key only
    // (the returned leaf has no private key — the holder pairs it with its own key).
    using var key = keyService.GenerateRsaKey(2048);
    var issued = ca.IssueComponentCertificate(componentId, fqdn, key);
    File.WriteAllText(crtPath, issued.Leaf.ExportCertificatePem());
    File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem());
    Console.WriteLine($"Issued client (leaf-only) cert for '{fqdn}' ({componentId}): {crtPath}, {keyPath}");
}

void TestBus(string bundlePath, string connectionString)
{
    var bundle = new X509Certificate2Collection();
    bundle.ImportFromPemFile(bundlePath);

    // Issue a fresh component client cert (leaf) + key, exactly like enrollment delivers.
    using var key = keyService.GenerateRsaKey(2048);
    var issued = ca.IssueComponentCertificate("bus-test", "bus-test.localhost", key);
    var leafPem = issued.Leaf.ExportCertificatePem();
    var keyPem = key.ExportPkcs8PrivateKeyPem();

    // Three ways to materialise the client certificate for the TLS stack:
    var variants = new (string name, Func<X509Certificate2> make)[]
    {
        ("copyWithPrivateKey (Identity self-issue path today)",
            () => issued.Leaf.CopyWithPrivateKey(key)),
        ("pkcs12 EphemeralKeySet (FileComponentCertificateStore path)",
            () =>
            {
                using var bound = X509Certificate2.CreateFromPem(leafPem, keyPem);
                return X509CertificateLoader.LoadPkcs12(
                    bound.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.EphemeralKeySet);
            }),
        ("pkcs12 MachineKeySet|PersistKeySet (Schannel-friendly, persists+admin)",
            () =>
            {
                using var bound = X509Certificate2.CreateFromPem(leafPem, keyPem);
                return X509CertificateLoader.LoadPkcs12(
                    bound.Export(X509ContentType.Pkcs12), null,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
            }),
        ("pkcs12 DefaultKeySet (user, no persist)",
            () =>
            {
                using var bound = X509Certificate2.CreateFromPem(leafPem, keyPem);
                return X509CertificateLoader.LoadPkcs12(
                    bound.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.DefaultKeySet);
            }),
        ("pkcs12 UserKeySet (user, no persist)",
            () =>
            {
                using var bound = X509Certificate2.CreateFromPem(leafPem, keyPem);
                return X509CertificateLoader.LoadPkcs12(
                    bound.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.UserKeySet);
            }),
        ("pkcs12 MachineKeySet (no persist)",
            () =>
            {
                using var bound = X509Certificate2.CreateFromPem(leafPem, keyPem);
                return X509CertificateLoader.LoadPkcs12(
                    bound.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.MachineKeySet);
            }),
    };

    foreach (var (name, make) in variants)
    {
        Console.WriteLine($"\n--- client cert variant: {name} ---");
        X509Certificate2 cert;
        try { cert = make(); }
        catch (Exception ex) { Console.WriteLine($"  build FAILED: {ex.GetType().Name}: {ex.Message}"); continue; }

        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            Ssl = new SslOption
            {
                Enabled = true,
                ServerName = "localhost",
                Certs = [cert],
                CertificateValidationCallback = (_, c, ch, e) =>
                    TrustEvaluation.IsTrustedServerCertificate(c!, ch, e, bundle),
            },
        };

        try
        {
            using var conn = factory.CreateConnection();
            using var model = conn.CreateModel();
            Console.WriteLine($"  CONNECTED, channel open={model.IsOpen}  (hasPrivKey={cert.HasPrivateKey})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CONNECT FAILED (hasPrivKey={cert.HasPrivateKey}): {ex.GetType().Name}: {ex.Message}");
            for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
                Console.WriteLine($"    inner: {inner.GetType().Name}: {inner.Message}");
        }
        finally { cert.Dispose(); }
    }
}

void TestTransport(string bundlePath, string connectionString)
{
    Environment.SetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING", connectionString);

    var bundle = new X509Certificate2Collection();
    bundle.ImportFromPemFile(bundlePath);

    using var key = keyService.GenerateRsaKey(2048);
    var issued = ca.IssueComponentCertificate("transport-test", "transport-test.localhost", key);
    using var bound = issued.Leaf.CopyWithPrivateKey(key);
    var pfxPath = Path.Combine(Path.GetTempPath(), "eryph-transport-test.pfx");
    File.WriteAllBytes(pfxPath, bound.Export(X509ContentType.Pkcs12));

    // Exactly the transport the app builds (Identity self-issue / component enrollment both end here).
    var transport = new RabbitMqRebusTransportConfigurer(pfxPath);

    using var activator = new Rebus.Activation.BuiltinHandlerActivator();
    try
    {
        Rebus.Config.Configure.With(activator)
            .Transport(t => transport.Configure(t, "eryph.transport-test"))
            .Start();
        Console.WriteLine("  TRANSPORT CONNECTED via RabbitMqRebusTransportConfigurer (bus started).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  TRANSPORT FAILED: {ex.GetType().Name}: {ex.Message}");
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
            Console.WriteLine($"    inner: {inner.GetType().Name}: {inner.Message}");
    }
}

void TestSsl(string connectionString, string policyMode)
{
    Environment.SetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING", connectionString);

    // Issue a client cert + key, export to a temp PFX (what SslSettings.CertPath consumes).
    using var key = keyService.GenerateRsaKey(2048);
    var issued = ca.IssueComponentCertificate("ssl-test", "ssl-test.localhost", key);
    using var bound = issued.Leaf.CopyWithPrivateKey(key);
    var pfxPath = Path.Combine(Path.GetTempPath(), "eryph-ssl-test.pfx");
    const string pass = "x";
    File.WriteAllBytes(pfxPath, bound.Export(X509ContentType.Pkcs12, pass));

    var policy = policyMode == "chain"
        ? System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors
        : System.Net.Security.SslPolicyErrors.None;
    var ssl = new Rebus.RabbitMq.SslSettings(
        enabled: true, serverName: "localhost",
        certificatePath: pfxPath, certPassphrase: pass,
        version: System.Security.Authentication.SslProtocols.None, // OS picks Tls12/Tls13
        acceptablePolicyErrors: policy);

    Console.WriteLine($"--- SslSettings CertPath, acceptablePolicyErrors={policy} ---");
    using var activator = new Rebus.Activation.BuiltinHandlerActivator();
    try
    {
        Rebus.Config.Configure.With(activator)
            .Transport(t => Rebus.Config.RabbitMqConfigurationExtensions
                .UseRabbitMq(t, connectionString, "eryph.ssl-test").Ssl(ssl))
            .Start();
        Console.WriteLine("  SSL CONNECTED (bus started).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  SSL FAILED: {ex.GetType().Name}: {ex.Message}");
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
            Console.WriteLine($"    inner: {inner.GetType().Name}: {inner.Message}");
    }
}

void EnrollTest(string identityUrl, string secret)
{
    using var rsa = System.Security.Cryptography.RSA.Create(2048);
    var request = new Eryph.ModuleCore.Components.ComponentEnrollmentRequest
    {
        ComponentType = Eryph.Messages.Components.ComponentType.Controller,
        Fqdn = "enroll-test.localhost",
        PublicKey = rsa.ExportSubjectPublicKeyInfo(),
        Token = secret,
    };
    var jsonOpts = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
    };
    jsonOpts.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    var json = System.Text.Json.JsonSerializer.Serialize(request, jsonOpts);
    var enrollUri = new Uri(new Uri(new Uri(identityUrl).GetLeftPart(UriPartial.Authority)), "/v1/components/enroll");
    using var http = new System.Net.Http.HttpClient();
    using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
    var resp = http.PostAsync(enrollUri, content).GetAwaiter().GetResult();
    var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    Console.WriteLine($"POST {enrollUri} (secret='{secret}') -> {(int)resp.StatusCode} {resp.StatusCode}");
    Console.WriteLine($"  body: {(body.Length > 300 ? body[..300] + "..." : body)}");
}

void IssueServer(string dns, string crtPath, string keyPath)
{
    var provider = new CaServerCertificateProvider(keyService, ca);
    var issued = provider.GetServerCertificate(dns);

    using var w = new StreamWriter(crtPath, append: false);
    w.WriteLine(issued.Leaf.ExportCertificatePem());
    foreach (var c in issued.IssuingChain)
        w.WriteLine(c.ExportCertificatePem());

    using var rsa = issued.Leaf.GetRSAPrivateKey()
        ?? throw new InvalidOperationException("Issued server certificate has no private key.");
    File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());

    Console.WriteLine(
        $"Issued server cert for '{dns}': {crtPath} (leaf + {issued.IssuingChain.Count} chain cert(s)), key {keyPath}");
}

string Arg(int i, string name) =>
    args.Length > i ? args[i] : throw new ArgumentException($"missing argument: {name}");

var command = args.Length > 0 ? args[0] : "status";
switch (command)
{
    case "status":
        Status();
        break;
    case "init":
        Init();
        break;
    case "export-bundle":
        ExportBundle(Arg(1, "ca-bundle.pem"));
        break;
    case "issue-server":
        IssueServer(Arg(1, "dns"), Arg(2, "crt.pem"), Arg(3, "key.pem"));
        break;
    case "issue-client":
        IssueClient(Arg(1, "componentId"), Arg(2, "fqdn"), Arg(3, "crt.pem"), Arg(4, "key.pem"));
        break;
    case "test-bus":
        TestBus(Arg(1, "ca-bundle.pem"), args.Length > 2 ? args[2] : "amqps://guest:guest@localhost:5671");
        break;
    case "test-transport":
        TestTransport(Arg(1, "ca-bundle.pem"), args.Length > 2 ? args[2] : "amqps://guest:guest@localhost:5671");
        break;
    case "test-ssl":
        TestSsl(args.Length > 1 ? args[1] : "amqps://guest:guest@localhost:5671", args.Length > 2 ? args[2] : "none");
        break;
    case "enroll-test":
        EnrollTest(args.Length > 1 ? args[1] : "http://localhost:8080/", args.Length > 2 ? args[2] : "devenrollmentsecret");
        break;
    case "provision-broker":
    {
        var dns = Arg(1, "dns");
        var outDir = Arg(2, "outDir");
        Directory.CreateDirectory(outDir);
        Init();
        ExportBundle(Path.Combine(outDir, "ca-bundle.pem"));
        IssueServer(dns, Path.Combine(outDir, "server.crt"), Path.Combine(outDir, "server.key"));
        break;
    }
    default:
        Console.Error.WriteLine($"unknown command: {command}");
        Environment.Exit(2);
        break;
}
