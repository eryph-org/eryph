using Eryph.VmManagement.Data.Core;

namespace Eryph.VmManagement.Data
{
    public sealed class VMComPortInfo : VirtualMachineDeviceInfo
    {
        public string Path { get; set; }

        //public OnOffState DebuggerMode { get; set; }
    }
}