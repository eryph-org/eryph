namespace Eryph.VmManagement.Data.Core;

/// <summary>
/// Contains information about the security settings of a Hyper-V VM
/// as returned by <c>Get-VMSecurity</c>.
/// </summary>
public class VMSecurityInfo
{
    public bool TpmEnabled { get; init; }

    public bool KsdEnabled { get; init; }

    public bool Shielded { get; init; }
    
    public bool EncryptStateAndVmMigrationTraffic { get; init; }
    
    public bool VirtualizationBasedSecurityOptOut { get; init; }
    
    public bool BindToHostTpm { get; init; }
}