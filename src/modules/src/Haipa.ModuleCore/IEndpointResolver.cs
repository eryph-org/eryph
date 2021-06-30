using System;

namespace Haipa.ModuleCore
{
    public interface IEndpointResolver
    {
        Uri GetEndpoint(string name);
    }
}