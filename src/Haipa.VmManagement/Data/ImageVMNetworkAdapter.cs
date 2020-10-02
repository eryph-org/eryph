using System;

namespace Haipa.VmManagement.Data
{
    public class ImageVMNetworkAdapter : VirtualMachineDeviceInfo
    {
        public bool IsLegacy { get; private set; }

        public string SwitchName { get; set; }

        public string AdapterId { get; private set; }

        public OnOffState MacAddressSpoofing { get; private set; }
        

        public OnOffState AllowTeaming { get; private set; }


    }
}