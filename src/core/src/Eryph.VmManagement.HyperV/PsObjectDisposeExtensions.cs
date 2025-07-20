using System;
using System.Management;
using System.Management.Automation;
using JetBrains.Annotations;

namespace Eryph.VmManagement;

public static class PsObjectDisposeExtensions
{
    public static void DisposeObject([CanBeNull] this PSObject psObject)
    {
        try
        {
            switch (psObject?.BaseObject)
            {
                case ManagementBaseObject managementObject:
                    // ManagementBaseObject.Dispose() does only work correctly when being
                    // invoked directly. The method is defined with the new keyword and will
                    // not be invoked via the IDisposable interface.
                    managementObject.Dispose();
                    return;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
        catch
        {
            // Ignore exceptions during disposal. This method is used to dispose a list
            // of objects, and we want to dispose as many as possible.
        }
    }
}
