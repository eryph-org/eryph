using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Runtime.Zero.HttpSys;
using Eryph.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Eryph.Runtime.Zero;

internal class ZeroStartupConfig
{
    public required IConfiguration Configuration { get; init; }

    public required string? BasePath { get; init; }

    public required ISSLEndpointManager SslEndpointManager { get; init; }

    public required ICryptoIOServices CryptoIO { get; init; }

    public required ICertificateGenerator CertificateGenerator { get; init; }

    public required string? OvsPackageDir { get; init; }
}
