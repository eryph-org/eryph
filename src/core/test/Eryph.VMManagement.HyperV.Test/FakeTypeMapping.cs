using System.Management.Automation;

namespace Eryph.VmManagement.Test;

public class FakeTypeMapping : ITypedPsObjectMapping
{
    public T Map<T>(PSObject psObject)
    {
        return (T) psObject.BaseObject;
    }
}