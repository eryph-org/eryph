using System.Collections.Generic;
using System.Net;

namespace Eryph.Runtime.Zero.HttpSys.SSLBinding;

public interface ICertificateBindingConfiguration
{
    IEnumerable<CertificateBinding> Query(IPEndPoint? ipPort = null);
    bool Bind(CertificateBinding binding);
    void Delete(IPEndPoint endPoint);
    void Delete(IPEndPoint[] endPoints);
}