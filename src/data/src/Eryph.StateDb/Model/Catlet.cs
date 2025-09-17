using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class Catlet : Resource
{
    public string? AgentName { get; set; }

    /// <summary>
    /// The last time the catlet has been inventoried.
    /// </summary>
    public DateTimeOffset LastSeen { get; set; }

    public CatletStatus Status { get; set; }

    /// <summary>
    /// The last time the <see cref="Status"/> and <see cref="UpTime"/>
    /// of the catlet have been observed.
    /// </summary>
    /// <remarks>
    /// The <see cref="Status"/> and <see cref="UpTime"/> can be updated
    /// independently of the inventory of the catlet. Hence, we track their
    /// observation time separately as well.
    /// </remarks>
    public DateTimeOffset LastSeenState { get; set; }

    public CatletType CatletType { get; set; }

    public ICollection<ReportedNetwork> ReportedNetworks { get; set; } = null!;

    public TimeSpan UpTime { get; set; }

    public Guid VmId { get; set; }

    public Guid MetadataId { get; set; }

    public string? Path { get; set; }

    public string? StorageIdentifier { get; set; }

    public required string DataStore { get; set; }

    public bool Frozen { get; set; }

    /// <summary>
    /// The catlet has been created with an old version of eryph and
    /// is missing important metadata. Hence, it cannot be edited
    /// and its configuration cannot be inspected.
    /// </summary>
    public bool IsDeprecated { get; set; }

    public Guid? HostId { get; set; }

    public CatletFarm? Host { get; set; }

    public List<CatletNetworkAdapter> NetworkAdapters { get; set; } = null!;

    public List<CatletDrive> Drives { get; set; } = null!;

    public int CpuCount { get; set; }

    public long StartupMemory { get; set; }

    public long MinimumMemory { get; set; }

    public long MaximumMemory { get; set; }

    public string? SecureBootTemplate { get; set; }

    public ISet<CatletFeature> Features { get; set; } = new HashSet<CatletFeature>();
}
