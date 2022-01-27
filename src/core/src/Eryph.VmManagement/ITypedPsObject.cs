using System.Management.Automation;

namespace Eryph.VmManagement;

public interface ITypedPsObject
{
    public PSObject PsObject { get; }
    public object Value { get; }
}