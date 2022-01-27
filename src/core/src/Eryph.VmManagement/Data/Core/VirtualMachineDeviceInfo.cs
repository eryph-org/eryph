using LanguageExt;

namespace Eryph.VmManagement.Data.Core
{
    public abstract class VirtualMachineDeviceInfo : Record<VirtualMachineDeviceInfo>
    {
        public virtual string Name { get; set; }

        public virtual string Id { get; set; }
        public virtual string DeviceId { get; set; }

    }
}