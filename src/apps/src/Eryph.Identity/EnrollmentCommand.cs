using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                    "usage: eryph-identity new-enrollment --type <ComponentType> --fqdn <host> --endpoint <https-url> [--out <file>] [--ttl-hours N]"
                    + "\n  (--endpoint may be omitted if ERYPH_IDENTITY_URL is set; it must be an absolute https:// URL)"
                    + "\n  --fqdn binds the token to one host: only the component whose FQDN matches may enroll with it.");
                return Task.FromResult(2);
            }

            // Use the SAME backend the daemon is configured with, or the minted file would be signed by
            // a different CA than the running identity host serves (e.g. ERYPH_PKI_KEYSTORE=file).
            var (useFile, pkiDirectory) = PkiOptions.Resolve();
            var ca = useFile
                ? new ComponentCertificateAuthority(
                    new FileCertificateStoreService(pkiDirectory),
                    new CertificateGenerator(),
                    new FileCertificateKeyService(PkiOptions.KeyDirectory(pkiDirectory)))
                : new ComponentCertificateAuthority(
                    new WindowsCertificateStoreService(),
                    new WindowsCertificateGenerator(),
                    new WindowsCertificateKeyService());

            var expiresAt = DateTimeOffset.UtcNow.AddHours(options.TtlHours);
            var token = EnrollmentTokenCodec.Issue(ca, options.ComponentType, options.Fqdn, expiresAt);

            // Pin the same root that signs tokens (the first with a usable private key — see
            // EnrollmentTokenCodec). Picking just the first trusted root could pin a public-only/older
            // root during rollover, leaving the component unable to verify the token or the identity TLS.
            var roots = ca.GetTrustedCaCertificates();
            var caCertificate = roots.FirstOrDefault(c => c.HasPrivateKey) ?? roots.FirstOrDefault()
                ?? throw new InvalidOperationException("The component CA is not available on this host.");

            var file = new ComponentEnrollmentFile
            {
                ComponentType = options.ComponentType,
                Fqdn = options.Fqdn,
                IdentityEndpoint = options.Endpoint,
                IdentityCaCertificate = caCertificate.Export(X509ContentType.Cert),
                Token = token,
                ExpiresAt = expiresAt,
            };

            var json = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            };
            json.Converters.Add(new JsonStringEnumConverter());
            // Ensure the output directory exists (owner-only on Unix — it will hold a token file) so a
            // missing --out directory fails clearly rather than throwing from the file write.
            var outDirectory = Path.GetDirectoryName(Path.GetFullPath(options.OutPath));
            if (!string.IsNullOrEmpty(outDirectory))
                SecureFile.CreateOwnerOnlyDirectory(outDirectory);

            // The file holds a live one-time token until redeemed — write it owner-only on either OS:
            // a restrictive Windows ACL, or 0600 on Unix.
            var contents = JsonSerializer.Serialize(file, json);
            if (OperatingSystem.IsWindows())
                WriteRestricted(options.OutPath, contents);
            else
                SecureFile.WriteOwnerOnly(options.OutPath, Encoding.UTF8.GetBytes(contents));

            Console.WriteLine(
                $"Wrote enrollment file for {options.ComponentType} on host '{options.Fqdn}' to '{options.OutPath}' (token valid until {file.ExpiresAt:u}).");
            return Task.FromResult(0);
        }

        // The file carries a live one-time enrollment token until it is redeemed, so it must never be
        // world-readable while it waits to be delivered. Create it with a restrictive ACL already in
        // place (no window where it exists with inherited permissions), locked to the creating user
        // plus local Administrators/SYSTEM, and remove a partial file if the write fails so a token is
        // never left behind under loose permissions.
        [SupportedOSPlatform("windows")]
        private static void WriteRestricted(string path, string contents)
        {
            using var identity = WindowsIdentity.GetCurrent();
            var owner = identity.User
                ?? throw new InvalidOperationException("Cannot determine the current user to restrict the enrollment file.");

            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (var sid in new[]
                     {
                         owner,
                         new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                         new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                     })
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    sid, FileSystemRights.FullControl, AccessControlType.Allow));
            }

            // Start from a fresh file so the security descriptor is applied at creation time.
            if (File.Exists(path))
                File.Delete(path);
            try
            {
                using var stream = new FileInfo(path).Create(
                    FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, security);
                using var writer = new StreamWriter(stream);
                writer.Write(contents);
            }
            catch
            {
                if (File.Exists(path))
                    File.Delete(path);
                throw;
            }
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
                || !Enum.TryParse<ComponentType>(typeText, ignoreCase: true, out var componentType)
                || !Enum.IsDefined(componentType))
                return null;

            // Require the bound host FQDN: the token authorizes enrollment of this type only for the
            // host that reports this FQDN, so an automated rollout cuts one file per host. Validate it
            // as a DNS name here so a typo cannot mint a token that no host could ever redeem; store it
            // lower-cased to match how it is bound and compared at redeem time.
            var fqdn = map.GetValueOrDefault("fqdn");
            if (string.IsNullOrWhiteSpace(fqdn) || !IsValidDnsName(fqdn))
                return null;
            fqdn = fqdn.ToLowerInvariant();

            // Require an explicit endpoint: the file is delivered to a remote component, so a silent
            // localhost default would embed the wrong address. It must be an absolute HTTPS URL — the
            // component validates the identity TLS endpoint against the pinned CA, so http:// (plaintext)
            // or a malformed URL would defeat the enrollment trust.
            var endpoint = map.GetValueOrDefault("endpoint")
                ?? Environment.GetEnvironmentVariable("ERYPH_IDENTITY_URL");
            if (string.IsNullOrWhiteSpace(endpoint)
                || !Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
                || endpointUri.Scheme != Uri.UriSchemeHttps)
                return null;
            var outPath = map.GetValueOrDefault("out") ?? $"{componentType}-enrollment.json";
            var ttlHours = map.TryGetValue("ttl-hours", out var ttlText) && int.TryParse(ttlText, out var t) ? t : 1;
            // A non-positive TTL would mint an already-expired, unredeemable token; reject it loudly.
            if (ttlHours <= 0)
                return null;

            return new Options(componentType, fqdn, endpoint, outPath, ttlHours);
        }

        // Mirror the server-side DNS-name check (ComponentEnrollmentService.IsValidDnsName) so the
        // bound FQDN is a syntactically valid hostname: labels of letters/digits/hyphens (no
        // leading/trailing hyphen), dot-separated, total <= 253. Rejects wildcards and whitespace.
        private static bool IsValidDnsName(string name) =>
            name.Length <= 253
            && Regex.IsMatch(
                name,
                @"^(?!-)[A-Za-z0-9-]{1,63}(?<!-)(\.(?!-)[A-Za-z0-9-]{1,63}(?<!-))*$",
                RegexOptions.CultureInvariant);

        private sealed record Options(
            ComponentType ComponentType, string Fqdn, string Endpoint, string OutPath, int TtlHours);
    }
}
