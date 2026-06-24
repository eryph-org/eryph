using System;
using System.IO;
using Eryph.Security.Cryptography;

namespace Eryph.Identity;

/// <summary>
/// Resolves which PKI key/cert backend the identity host uses, so the running daemon and the
/// <c>new-enrollment</c> command always agree (a mismatch would mint an enrollment file against a
/// different CA than the daemon serves). This only reads configuration — it does not create or
/// register services; the daemon registers them in DI, the command constructs them directly.
/// <c>ERYPH_PKI_KEYSTORE</c> = <c>auto</c> (default; windows on Windows, file otherwise) |
/// <c>windows</c> | <c>file</c>; directory from <c>ERYPH_PKI_DIRECTORY</c>.
/// </summary>
internal static class PkiOptions
{
    public static (bool UseFile, string Directory) Resolve()
    {
        var choice = Environment.GetEnvironmentVariable("ERYPH_PKI_KEYSTORE")?.Trim().ToLowerInvariant();
        var useFile = choice switch
        {
            null or "" or "auto" => !OperatingSystem.IsWindows(),
            "file" => true,
            "windows" => OperatingSystem.IsWindows()
                ? false
                : throw new InvalidOperationException(
                    "ERYPH_PKI_KEYSTORE=windows requires Windows; use 'file' (or 'auto') on this platform."),
            _ => throw new InvalidOperationException(
                $"Unsupported ERYPH_PKI_KEYSTORE '{choice}'. Use 'auto', 'windows' or 'file'."),
        };

        var directory = Environment.GetEnvironmentVariable("ERYPH_PKI_DIRECTORY") is { Length: > 0 } dir
            ? dir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "eryph", "pki");

        return (useFile, directory);
    }

    /// <summary>The key directory used by the file backend (keys live under a 'keys' subdirectory).</summary>
    public static string KeyDirectory(string directory) => Path.Combine(directory, "keys");

    /// <summary>
    /// Constructs the platform certificate/key services for the resolved backend. The single place
    /// the backend switch lives, so the DI registration (module consumers) and the Kestrel TLS
    /// listeners — which run outside the module container and must build the services themselves —
    /// always pick the same backend and therefore the same CA.
    /// </summary>
    public static (ICertificateKeyService Keys, ICertificateStoreService Store, ICertificateGenerator Generator)
        CreateServices()
    {
        var (useFile, directory) = Resolve();
        return useFile
            ? (new FileCertificateKeyService(KeyDirectory(directory)),
                new FileCertificateStoreService(directory),
                new CertificateGenerator())
            : (new WindowsCertificateKeyService(),
                new WindowsCertificateStoreService(),
                new WindowsCertificateGenerator());
    }
}
