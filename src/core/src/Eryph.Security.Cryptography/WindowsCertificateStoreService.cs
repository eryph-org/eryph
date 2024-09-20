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
        AddToStore(certificate, StoreName.My);
    }

    public void AddToRootStore(X509Certificate2 certificate)
    {
        AddToStore(certificate, StoreName.Root);
    }

    private static void AddToStore(X509Certificate2 certificate, StoreName storeName)
    {
        using var machineStore = new X509Store(storeName, StoreLocation.LocalMachine);
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

    public void RemoveFromMyStore(PublicKey subjectKey)
    {
        RemoveFromStore(subjectKey, StoreName.My);
    }

    public void RemoveFromMyStore(X500DistinguishedName subjectName)
    {
        RemoveFromStore(subjectName, StoreName.My);
    }

    public void RemoveFromRootStore(PublicKey subjectKey)
    {
        RemoveFromStore(subjectKey, StoreName.Root);
    }

    public void RemoveFromRootStore(X500DistinguishedName subjectName)
    {
        RemoveFromStore(subjectName, StoreName.Root);
    }

    private static void RemoveFromStore(X500DistinguishedName subjectName, StoreName storeName)
    {
        using var machineStore = new X509Store(storeName, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        var certificates = machineStore.Certificates.Find(
            X509FindType.FindBySubjectDistinguishedName,
            subjectName.Name,
            false);
        machineStore.RemoveRange(certificates);
    }

    private static void RemoveFromStore(PublicKey subjectKey, StoreName storeName)
    {
        var extension = new X509SubjectKeyIdentifierExtension(subjectKey, false);
        using var machineStore = new X509Store(storeName, StoreLocation.LocalMachine);
        machineStore.Open(OpenFlags.ReadWrite);
        var certificates = machineStore.Certificates.Find(
            X509FindType.FindBySubjectKeyIdentifier,
            extension.SubjectKeyIdentifier!,
            false);
        machineStore.RemoveRange(certificates);
    }
}
