using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public class VirtualCatlet : Catlet
    {
        public VirtualCatlet()
        {
            CatletType = CatletType.VM;
        }

        public Guid VMId { get; set; }

        public Guid MetadataId { get; set; }

        public string Path { get; set; }
        public string StorageIdentifier { get; set; }
        public string DataStore { get; set; }
        public string Environment { get; set; }
        public bool Frozen { get; set; }
        public virtual VirtualCatletHost Host { get; set; }


        public virtual List<VirtualCatletNetworkAdapter> NetworkAdapters { get; set; }

        public virtual List<VirtualCatletDrive> Drives { get; set; }

        public int CpuCount { get; set; }

        public long StartupMemory { get; set; }
        public long MinimumMemory { get; set; }
        public long MaximumMemory { get; set; }

        public string SecureBootTemplate { get; set; }

        public List<VCatletFeature> Features { get; set; }
    }


    public enum VCatletFeature
    {
        SecureBoot,
        DynamicMemory,
        NestedVirtualization
    }

}