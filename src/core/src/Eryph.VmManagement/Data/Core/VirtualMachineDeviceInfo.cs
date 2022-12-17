using Eryph.ConfigModel;
using Eryph.Core;
using LanguageExt;

namespace Eryph.VmManagement.Data.Core
{
    public class VirtualMachineDeviceInfo
    {
        [PrivateIdentifier]
        public virtual string Name { get; set; }

        [PrivateIdentifier]
        public virtual string Id { get; set; }


    }
}