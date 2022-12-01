// See https://aka.ms/new-console-template for more information

using System.DirectoryServices.ActiveDirectory;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Effects.Traits;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static LanguageExt.Prelude;

Console.WriteLine("Hello, World!");



var i = 2;

do
{
    var ps = new PowershellEngine(new NullLoggerFactory().CreateLogger(""));
    var runtime = new ConsoleRuntime(new NullLoggerFactory(), ps, new CancellationTokenSource());

    Console.WriteLine($"*** LEFT RUNS: {i} ****");
    var res = await getHostState<ConsoleRuntime>().Run(runtime);
    res.Match(s => Console.WriteLine(s), e => Console.WriteLine(e));

    ps.Dispose();
    GC.Collect();

    i--;

} while (i > 0);

Console.WriteLine("** DONE **");

GC.Collect();

Console.ReadLine();


static Aff<RT, HostState> getHostState<RT>() where RT : struct, HasHostNetworkCommands<RT>,
    HasCancel<RT> =>

    use(default(RT).HostNetworkCommands.Bind(
        c => c.GetCimSession()),
        cimSession => 
            from hostCommands in default(RT).HostNetworkCommands

        // collect network state of host
        from vmSwitchExtensions in hostCommands.GetSwitchExtensions(cimSession)
        from vmSwitches in hostCommands.GetSwitches(cimSession)
        from netAdapters in hostCommands.GetPhysicalAdapters(cimSession)
        from allAdapterNames in hostCommands.GetAdapterNames(cimSession)
        from overlaySwitch in hostCommands.FindOverlaySwitch(cimSession, vmSwitches, vmSwitchExtensions,
            netAdapters)
        from netNat in hostCommands.GetNetNat(cimSession)
        let hostState = new HostState(vmSwitchExtensions, vmSwitches, netAdapters, allAdapterNames, overlaySwitch,
            netNat)
        from _ in hostCommands.RemoveCimSession(cimSession)
        select hostState);
    // tools
