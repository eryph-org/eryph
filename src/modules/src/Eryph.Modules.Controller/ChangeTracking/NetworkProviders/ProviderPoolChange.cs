using System.Collections.Generic;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal class ProviderPoolChange
{
    public List<string> ProviderNames { get; set; } = new();
}