using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public class Catlet : Resource
    {
        public string AgentName { get; set; }

        public CatletStatus Status { get; set; }
        public DateTime StatusTimestamp { get; set; }

        public CatletType CatletType { get; set; }

        public virtual ICollection<ReportedNetwork> ReportedNetworks { get; set; }

        public TimeSpan? UpTime { get; set; }

        public Guid VMId { get; set; }

        public Guid MetadataId { get; set; }

        public string Path { get; set; }
        public string StorageIdentifier { get; set; }
        public string DataStore { get; set; }
        public string Environment { get; set; }
        public bool Frozen { get; set; }
        public virtual CatletFarm Host { get; set; }

        public virtual List<CatletNetworkAdapter> NetworkAdapters { get; set; }

        public virtual List<CatletDrive> Drives { get; set; }

        public int CpuCount { get; set; }

        public long StartupMemory { get; set; }
        public long MinimumMemory { get; set; }
        public long MaximumMemory { get; set; }

        public string SecureBootTemplate { get; set; }

        public List<CatletFeature> Features { get; set; }
    }
}
