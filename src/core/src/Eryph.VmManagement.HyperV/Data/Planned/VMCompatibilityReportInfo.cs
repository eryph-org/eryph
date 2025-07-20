using LanguageExt;

namespace Eryph.VmManagement.Data.Planned
{
    public class VMCompatibilityReportInfo : Record<VMCompatibilityReportInfo>
    {
        public PlannedVirtualMachineInfo VM { get; private set; }
    }
}