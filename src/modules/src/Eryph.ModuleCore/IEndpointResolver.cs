using System;

namespace Eryph.ModuleCore
{
    public interface IEndpointResolver
    {
        Uri GetEndpoint(string name);
    }
}