using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;

namespace Eryph.Identity
{
    /// <summary>
    /// The operator command that produces a component enrollment file (run on the identity host,
    /// elevated — it uses the component CA in the machine store). Usage:
    /// <code>eryph-identity new-enrollment --type &lt;ComponentType&gt; [--endpoint &lt;url&gt;] [--out &lt;file&gt;] [--ttl-hours N]</code>
    /// The resulting file bundles the identity CA certificate (trust anchor), the endpoint and a
    /// one-time token; deliver it out-of-band to the component, which imports it to enroll.
    /// </summary>
    internal static class EnrollmentCommand
    {
        public const string Verb = "new-enrollment";

        public static Task<int> RunAsync(string[] args)
        {
            var options = ParseArgs(args);
            if (options is null)
            {
                Console.Error.WriteLine(
                    "usage: eryph-identity new-enrollment --type <ComponentType> [--endpoint <url>] [--out <file>] [--ttl-hours N]");
                return Task.FromResult(2);
            }

            var ca = new ComponentCertificateAuthority(
                new WindowsCertificateStoreService(),
                new WindowsCertificateGenerator(),
                new WindowsCertificateKeyService());
            var tokenService = new EnrollmentTokenService(ca);

            var ttl = TimeSpan.FromHours(options.TtlHours);
            var token = tokenService.Mint(options.ComponentType, ttl);

            var caCertificate = ca.GetTrustedCaCertificates().FirstOrDefault()
                ?? throw new InvalidOperationException("The component CA is not available on this host.");

            var file = new ComponentEnrollmentFile
            {
                ComponentType = options.ComponentType,
                IdentityEndpoint = options.Endpoint,
                IdentityCaCertificate = caCertificate.Export(X509ContentType.Cert),
                Token = token,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            };

            var json = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            };
            json.Converters.Add(new JsonStringEnumConverter());
            File.WriteAllText(options.OutPath, JsonSerializer.Serialize(file, json));

            Console.WriteLine(
                $"Wrote enrollment file for {options.ComponentType} to '{options.OutPath}' (token valid until {file.ExpiresAt:u}).");
            return Task.FromResult(0);
        }

        private static Options? ParseArgs(string[] args)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // args[0] is the verb; parse the remaining --key value pairs.
            for (var i = 1; i + 1 < args.Length; i += 2)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    return null;
                map[args[i][2..]] = args[i + 1];
            }

            if (!map.TryGetValue("type", out var typeText)
                || !Enum.TryParse<ComponentType>(typeText, ignoreCase: true, out var componentType))
                return null;

            var endpoint = map.GetValueOrDefault("endpoint")
                ?? Environment.GetEnvironmentVariable("ERYPH_IDENTITY_URL")
                ?? "https://localhost:8080/";
            var outPath = map.GetValueOrDefault("out") ?? $"{componentType}-enrollment.json";
            var ttlHours = map.TryGetValue("ttl-hours", out var ttlText) && int.TryParse(ttlText, out var t) ? t : 1;

            return new Options(componentType, endpoint, outPath, ttlHours);
        }

        private sealed record Options(ComponentType ComponentType, string Endpoint, string OutPath, int TtlHours);
    }
}
