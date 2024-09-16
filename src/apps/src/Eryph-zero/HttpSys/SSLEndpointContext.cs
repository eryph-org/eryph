using System;
using Eryph.Runtime.Zero.HttpSys.SSLBinding;

namespace Eryph.Runtime.Zero.HttpSys;

public sealed class SSLEndpointContext : IDisposable
{
    private readonly ISSLEndpointRegistry _registry;
    private readonly Uri _url;
    private readonly CertificateBinding _binding;

    public SSLEndpointContext(ISSLEndpointRegistry registry, Uri url, CertificateBinding binding)
    {
        _registry = registry;
        _url = url;
        _binding = binding;
    }
    
    private void ReleaseUnmanagedResources()
    {
        _registry?.UnRegisterSSLEndpoint(_url, _binding);
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~SSLEndpointContext()
    {
        ReleaseUnmanagedResources();
    }
}