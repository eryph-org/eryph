using Haipa.VmManagement.Data.Core;

namespace Haipa.VmManagement.Data
{
    public sealed class VMFibreChannelHbaInfo : VirtualMachineDeviceInfo
    {

 
        public string SanName { get; set; }


        public string WorldWideNodeNameSetA { get; set; }


        public string WorldWidePortNameSetA { get; set; }


        public string WorldWideNodeNameSetB { get; set; }


        public string WorldWidePortNameSetB { get; set; }


    }
}