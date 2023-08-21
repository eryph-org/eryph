using System.Management.Automation;

namespace Eryph.VmManagement;

public interface ITypedPsObjectMapping
{
    T Map<T>(PSObject psObject);
}