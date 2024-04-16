using System.Collections.Generic;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal class FloatingNetworkPortChange
{
    public List<string> ProviderNames { get; init; } = new();
}
