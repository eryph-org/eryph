using System;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    internal class GuestNetworkAdapterChangedEvent
    {
        public Guid VmId { get; set; }
        public string AdapterId { get; set; }


    }
}