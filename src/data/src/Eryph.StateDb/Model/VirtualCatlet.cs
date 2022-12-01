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
        public virtual VirtualCatletHost Host { get; set; }


        public virtual List<VirtualCatletNetworkAdapter> NetworkAdapters { get; set; }

        public virtual List<VirtualMachineDrive> Drives { get; set; }


    }
}