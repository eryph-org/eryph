using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using Microsoft.PowerShell;

namespace Eryph.VmManagement.Sys;

public interface DismIO
{
    public ValueTask<Seq<DismDriverInfo>> GetInstalledDriverPackages();
}

public readonly struct LiveDismIO : DismIO
{
    public static readonly DismIO Default = new LiveDismIO();

    public async ValueTask<Seq<DismDriverInfo>> GetInstalledDriverPackages()
    {
        // Get-WindowsDriver fails on Windows Server 2019 when being executed
        // with our standard Powershell engine. Hence, we execute it separately
        // with the minimalistic code below.
        
        var iss = InitialSessionState.CreateDefault();
        iss.ExecutionPolicy = ExecutionPolicy.RemoteSigned;

        using var runspace = RunspaceFactory.CreateRunspacePool(iss);

        await Task.Factory.FromAsync(runspace.BeginOpen, runspace.EndOpen, null);

        using var ps = PowerShell.Create();
        ps.RunspacePool = runspace;

        using var psResult = await ps.AddCommand("Get-WindowsDriver").AddParameter("Online").InvokeAsync().ConfigureAwait(false);
        var error = ps.Streams.Error.FirstOrDefault();
        if (error is not null)
            throw new Exception("Could not get installed driver packages via Powershell", error.Exception);

        var driverInfos = psResult.ToArray().Map(psObject => new DismDriverInfo()
        {
            Driver = (string)psObject.Properties["Driver"].Value,
            OriginalFileName = (string)psObject.Properties["OriginalFileName"].Value,
            ProviderName = (string)psObject.Properties["ProviderName"].Value,
            MajorVersion = (uint)psObject.Properties["MajorVersion"].Value,
            MinorVersion = (uint)psObject.Properties["MinorVersion"].Value,
            Build = (uint)psObject.Properties["Build"].Value,
            Revision = (uint)psObject.Properties["Revision"].Value
        }).ToArray().ToSeq();

        psResult.Iter(psObject => psObject.DisposeObject());

        return driverInfos;
    }
}
