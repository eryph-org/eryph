using LanguageExt;

namespace Haipa.VmManagement.Data.Planned
{
    public class VMCompatibilityReportInfo : Record<VMCompatibilityReportInfo>
    {
        public PlannedVirtualMachineInfo VM { get; private set; }
    }
}