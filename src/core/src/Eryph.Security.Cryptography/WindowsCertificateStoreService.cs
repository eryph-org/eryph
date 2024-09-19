using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography;

[SupportedOSPlatform("windows")]
public class WindowsCertificateStoreService : ICertificateStoreService
{
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

    public void AddToRootStore(X509Certificate2 certificate)
    {
        using var machineStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        machineStore.Add(certificate);
    }

    public IReadOnlyList<X509Certificate2> GetFromMyStore(X500DistinguishedName subjectName)
    {
        using var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadOnly);

        return machineStore.Certificates.Find(
                X509FindType.FindBySubjectDistinguishedName,
                subjectName.Name,
                false)
            .ToList();
    }

    public IReadOnlyList<X509Certificate2> GetFromRootStore(X500DistinguishedName subjectName)
    {
        using var machineStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadOnly);

        return machineStore.Certificates.Find(
                X509FindType.FindBySubjectDistinguishedName,
                subjectName.Name,
                false)
            .ToList();
    }

    public void RemoveFromMyStore(X509Certificate2 certificate)
    {
        using var machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        machineStore.Remove(certificate);
    }


    public void RemoveFromRootStore(X509Certificate2 certificate)
    {
        using var machineStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        machineStore.Remove(certificate);
    }
}
