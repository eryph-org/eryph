using System;
using System.Management.Automation;

namespace Eryph.VmManagement;

public interface ITypedPsObject: IDisposable
{
    public PSObject PsObject { get; }
    public object Value { get; }
}