using Microsoft.Management.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Eryph.VmManagement.Data.Core;

/// <summary>
/// Contains information about the processor settings of a Hyper-V VM
/// as returned by <c>Get-VMProcessor</c>.
/// </summary>
public class VMProcessorInfo
{
    public string ResourcePoolName { get; init; }

    public long Count { get; init; }

    public bool CompatibilityForMigrationEnabled { get; init; }

    public bool CompatibilityForOlderOperatingSystemsEnabled { get; init; }

    public long HwThreadCountPerCore { get; init; }

    public bool ExposeVirtualizationExtensions { get; init; }

    public bool EnablePerfmonPmu { get; init; }

    public bool EnablePerfmonArchPmu { get; init; }

    public bool EnablePerfmonLbr { get; init; }

    public bool EnablePerfmonPebs { get; init; }

    public bool EnablePerfmonIpt { get; init; }

    public bool EnableLegacyApicMode { get; init; }

    public bool AllowACountMCount { get; init; }

    public string CpuBrandString { get; init; }

    public int PerfCpuFreqCapMhz { get; init; }

    public int L3CacheWays { get; init; }

    public long Maximum { get; init; }

    public long Reserve { get; init; }

    public int RelativeWeight { get; init; }

    public long MaximumCountPerNumaNode { get; init; }

    public long MaximumCountPerNumaSocket { get; init; }

    public bool EnableHostResourceProtection { get; init; }

    public string Id { get; init; }
}
