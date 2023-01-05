using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public class VirtualCatletHost : Catlet
    {
        public VirtualCatletHost()
        {
            CatletType = CatletType.VMHost;
        }

        public virtual ICollection<VirtualCatlet> VirtualCatlets { get; set; }

        public string HardwareId { get; set; }
    }
}