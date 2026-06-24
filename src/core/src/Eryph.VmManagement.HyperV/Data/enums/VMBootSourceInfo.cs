using LanguageExt;

namespace Eryph.VmManagement.Data.enums;

public class VMBootSourceInfo : Record<VMBootSourceInfo>
{
    public VMBootSourceType BootType { get; private set; }

    public string Description { get; private set; }
}
