using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;


namespace Eryph.Security.Cryptography;

[SupportedOSPlatform("windows")]
public class WindowsCertificateStoreService : ICertificateStoreService
{
    
    public void AddAsRootCertificate(X509Certificate certificate)
    {
        using var machineStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        
        var winCert = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));

        var storedCerts = machineStore.Certificates.Find(
            X509FindType.FindBySubjectDistinguishedName,
            winCert.SubjectName.Name, false);
            
        if(storedCerts.Count > 0)
            machineStore.RemoveRange(storedCerts);
            
        machineStore.Add(winCert);
    }
    
    
    public IEnumerable<X509Certificate> GetFromMyStore(X509Name issuerName)
    {
        using var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadOnly);


        return machineStore.Certificates.Find(X509FindType.FindByIssuerDistinguishedName,
                issuerName.ToString() ?? "", false)
            .Select(DotNetUtilities.FromX509Certificate);

    }

    public IReadOnlyList<X509Certificate2> GetFromMyStore2(X500DistinguishedName subjectName)
    {
        using var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadOnly);

        return machineStore.Certificates.Find(
                X509FindType.FindBySubjectDistinguishedName,
                subjectName.Name,
                false)
            .ToList();
    }

    public void AddToMyStore(X509Certificate2 certificate)
    {
        using var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);

        var storedCerts = machineStore.Certificates.Find(
            X509FindType.FindBySubjectDistinguishedName,
            certificate.SubjectName.Name,
            false);

        if (storedCerts.Count > 0)
            machineStore.RemoveRange(storedCerts);

        machineStore.Add(certificate);
        
    }

    public IEnumerable<X509Certificate> GetFromRootStore(X509Name distinguishedName)
    {
        using var machineStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadOnly);
        var dnString = distinguishedName.ToString();
        return machineStore.Certificates.Find(X509FindType.FindBySubjectDistinguishedName,
                dnString ?? "", false)
            .Select(DotNetUtilities.FromX509Certificate);

    }

    public void AddToMyStore(X509Certificate certificate, AsymmetricCipherKeyPair? keyPair=null)
    {
        using var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        var winCert = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));

        var storedCerts = machineStore.Certificates.Find(X509FindType.FindBySubjectDistinguishedName,
            winCert.SubjectName.Name, false);
            
        if(storedCerts.Count > 0)
            machineStore.RemoveRange(storedCerts);

        if (keyPair != null)
        {
            var rsaParams = (RsaPrivateCrtKeyParameters)keyPair.Private;
            var rsaKey = DotNetUtilities.ToRSA(rsaParams, new CspParameters
            {
                KeyContainerName = Guid.NewGuid().ToString(),
                KeyNumber = (int)KeyNumber.Exchange,
                Flags = CspProviderFlags.UseMachineKeyStore
            });
            var winCertWithKey = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));

            machineStore.Add(winCertWithKey.CopyWithPrivateKey(rsaKey));
            return;
        }

        
        machineStore.Add(winCert);

    }
    
            
    public void RemoveFromRootStore(X509Certificate certificate)
    {
        using var machineStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        var winCert = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));

        machineStore.Remove(winCert);
    }

    public void RemoveFromMyStore(X509Certificate certificate)
    {
        using var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        var winCert = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));

        machineStore.Remove(winCert);
    }
    
    public void RemoveFromMyStore2(X509Certificate2 certificate)
    {
        using var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        machineStore.Remove(certificate);
    }

    public bool IsValidRootCertificate(X509Certificate certificate)
    {
        using var machineStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadOnly);

        var winCert = new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));

        if (!winCert.Verify()) return false;
        var storedCert = machineStore.Certificates.Find(X509FindType.FindByThumbprint, winCert.Thumbprint!, true);
        return storedCert.Count >= 0;
    }
}