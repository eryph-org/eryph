using System.Management.Automation;

namespace Eryph.VmManagement;

public interface IPsObjectRegistry
{
    void AddPsObject(PSObject psObject);
}