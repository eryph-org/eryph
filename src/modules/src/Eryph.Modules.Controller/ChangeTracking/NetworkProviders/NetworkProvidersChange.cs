using System.Collections.Generic;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal class NetworkProvidersChange
{
    public List<string> ProviderNames { get; set; } = new();
}