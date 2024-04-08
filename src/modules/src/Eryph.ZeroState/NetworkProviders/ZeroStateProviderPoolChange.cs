using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState.NetworkProviders;

internal class ZeroStateProviderPoolChange
{
    public List<string> ProviderNames { get; set; } = new();
}