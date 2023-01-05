using System;
using System.Management.Automation;

namespace Eryph.VmManagement;

public static class PsObjectDisposeExtensions
{
    public static void DisposeObject(this PSObject psObject)
    {
        if (psObject?.BaseObject is not IDisposable disposable) return;

        try
        {
            disposable.Dispose();
        }
        catch
        {
            // ignored
        }
    }
}