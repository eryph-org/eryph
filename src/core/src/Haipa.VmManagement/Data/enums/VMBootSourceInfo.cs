using LanguageExt;

namespace Haipa.VmManagement.Data
{
    public class VMBootSourceInfo : Record<VMBootSourceInfo>
    {
        public VMBootSourceType BootType { get; private set; }

        public string Description { get; private set; }
    }
}