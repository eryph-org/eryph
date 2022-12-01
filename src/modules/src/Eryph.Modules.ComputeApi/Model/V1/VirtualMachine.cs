using System.Collections.Generic;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class VirtualCatlet : Catlet
    {
        public IEnumerable<VirtualCatletNetworkAdapter> NetworkAdapters { get; set; }

        public IEnumerable<VirtualCatletDrive> Drives { get; set; }
    }
}