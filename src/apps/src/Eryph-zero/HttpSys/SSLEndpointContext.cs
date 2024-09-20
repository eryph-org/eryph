using System;
using Eryph.Runtime.Zero.HttpSys.SSLBinding;

namespace Eryph.Runtime.Zero.HttpSys;

public sealed class SSLEndpointContext : IDisposable
{
    private readonly ISSLEndpointRegistry _registry;
    private readonly Uri _url;
    private readonly Guid _applicationId;

    public SSLEndpointContext(ISSLEndpointRegistry registry, Uri url, Guid applicationId)
    {
        _registry = registry;
        _url = url;
        _applicationId = applicationId;
    }
    
    private void ReleaseUnmanagedResources()
    {
        _registry?.UnRegisterSSLEndpoint(_url, _applicationId);
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