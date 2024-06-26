﻿using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class Catlet : Resource
{
    public string? AgentName { get; set; }

    public CatletStatus Status { get; set; }

    public DateTime StatusTimestamp { get; set; }

    public CatletType CatletType { get; set; }

    public ICollection<ReportedNetwork> ReportedNetworks { get; set; } = null!;

    public TimeSpan? UpTime { get; set; }

    public Guid VMId { get; set; }

    public Guid MetadataId { get; set; }

    public string? Path { get; set; }

    public string? StorageIdentifier { get; set; }

    public required string DataStore { get; set; }

    public bool Frozen { get; set; }

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
