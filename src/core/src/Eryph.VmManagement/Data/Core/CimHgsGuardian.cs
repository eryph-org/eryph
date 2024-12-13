using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eryph.VmManagement.Data.Core;

/// <summary>
/// Represents an HGS guardian as returned by the Cmdlet
/// <c>Get-HgsGuardian</c>.
/// </summary>
public class CimHgsGuardian
{
    [CanBeNull] public string Name { get; init; }

    public bool HasPrivateSigningKey { get; init; }

    public X509Certificate2 EncryptionCertificate { get; init; }

    public X509Certificate2 SigningCertificate { get; init; }
}
