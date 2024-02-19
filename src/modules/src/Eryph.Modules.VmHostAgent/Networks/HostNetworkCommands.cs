using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;



public class HostNetworkCommands<RT> : IHostNetworkCommands<RT>
    where RT : struct, HasPowershell<RT>, HasCancel<RT>
{
    public Aff<RT, Seq<VMSwitch>> GetSwitches()
    {
        return from psEngine in default(RT).Powershell.ToAff()
            from vmSwitches in psEngine.GetObjectsAsync<VMSwitch>(
                PsCommandBuilder.Create().AddCommand("Get-VMSwitch")).ToAff()
            from switches in vmSwitches.Map(s => Prelude.Try(() =>
            {
                var result = s.Value;

                // try to fetch interface ids
                result.NetAdapterInterfaceGuid = s.GetNetAdapterInterfaceGuid();
                return result;

            }).ToAff()).TraverseParallel(l=>l)
            select switches;
    }

    public Aff<RT, Option<VMSystemSwitchExtension>> GetInstalledSwitchExtension() =>
        from psEngine in default(RT).Powershell
        let command = PsCommandBuilder.Create().AddCommand("Get-VMSystemSwitchExtension")
        from results in psEngine.GetObjectsAsync<VMSystemSwitchExtension>(command).ToAff()
        select results.Select(r => r.Value)
            .Where(e => string.Equals(e.Name, EryphConstants.SwitchExtensionName, StringComparison.OrdinalIgnoreCase))
            .HeadOrNone();

    public Aff<RT, Seq<VMSwitchExtension>> GetSwitchExtensions() =>
        from psEngine in default(RT).Powershell
        from vmSwitchExtensions in psEngine.GetObjectsAsync<VMSwitchExtension>(
            PsCommandBuilder.Create()
                .AddCommand("Get-VMSwitch")
                .AddCommand("Get-VMSwitchExtension")
                .AddParameter("Name", EryphConstants.SwitchExtensionName)).ToAff()
        select vmSwitchExtensions.Map(s => s.ToValue());

    public Aff<RT, Unit> DisableSwitchExtension(Guid switchId) =>
        from psEngine in default(RT).Powershell
        from _ in psEngine.RunAsync(PsCommandBuilder.Create()
            .AddCommand("Get-VMSwitch")
            .AddParameter("Id", switchId)
            .AddCommand("Get-VMSwitchExtension")
            .AddParameter("Name", EryphConstants.SwitchExtensionName)
            .AddCommand("Disable-VMSwitchExtension")).ToAff()
        select unit;

    public Aff<RT, Unit> EnableSwitchExtension(Guid switchId) =>
        from psEngine in default(RT).Powershell
        from _ in psEngine.RunAsync(PsCommandBuilder.Create()
            .AddCommand("Get-VMSwitch")
            .AddParameter("Id", switchId)
            .AddCommand("Enable-VMSwitchExtension")
            .AddParameter("Name", EryphConstants.SwitchExtensionName)).ToAff()
        select unit;
    
    public Aff<RT, Seq<HostNetworkAdapter>> GetPhysicalAdapters() =>
        from psEngine in default(RT).Powershell.ToAff()
        from netAdapters in psEngine.GetObjectsAsync<HostNetworkAdapter>(
            PsCommandBuilder.Create()
                .AddCommand("Get-NetAdapter")
                .AddParameter("-Physical")).ToAff()
        select netAdapters.Map(s => s.ToValue());

    public Aff<RT, Seq<string>> GetAdapterNames() =>
        from psEngine in default(RT).Powershell.ToAff()
        from netAdapters in psEngine.GetObjectsAsync<HostNetworkAdapter>(
            PsCommandBuilder.Create()
                .AddCommand("Get-NetAdapter")).ToAff()
        select netAdapters.Map(s => s.ToValue().Name);


    public Aff<RT, Seq<NetNat>> GetNetNat() =>
        from psEngine in default(RT).Powershell.ToAff()
        from netNat in psEngine.GetObjectsAsync<NetNat>(
            PsCommandBuilder.Create().AddCommand("Get-NetNat")).ToAff()
        select netNat.Map(s => s.Value);

    public Aff<RT, Unit> EnableBridgeAdapter(string adapterName) =>
        default(RT).Powershell.Bind(rt =>
            rt.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Get-NetAdapter")
                    .AddArgument(adapterName)
                    .AddCommand("Enable-NetAdapter"))
                .ToAff());

    public Aff<RT, Unit> ConfigureNATAdapter(string adapterName, IPAddress ipAddress, IPNetwork network)
    {
        var ipCommand = PsCommandBuilder.Create()
            .AddCommand("New-NetIPAddress")
            .AddParameter("InterfaceAlias", adapterName)
            .AddParameter("IPAddress", ipAddress.ToString())
            .AddParameter("PrefixLength", network.Cidr);


        return from psEngine in default(RT).Powershell.ToAff()
            from uEnable in psEngine.RunAsync(
                PsCommandBuilder.Create()
                    .AddCommand("Get-NetAdapter")
                    .AddArgument(adapterName)
                    .AddCommand("Enable-NetAdapter")).ToAff()
            from uNoDhcp in psEngine.RunAsync(
                PsCommandBuilder.Create()
                    .AddCommand("Set-NetIPInterface")
                    .AddParameter("InterfaceAlias", adapterName)
                    .AddParameter("Dhcp", "Disabled")
                    .AddParameter("Confirm", false)).ToAff()
            from uRemoveGateway in psEngine.RunAsync(
                PsCommandBuilder.Create()
                    .AddCommand("Remove-NetRoute")
                    .AddParameter("InterfaceAlias", adapterName)
                    .AddParameter("Confirm", false)
                    .AddParameter("ErrorAction", "SilentlyContinue")).ToAff()
            from uRemoveIP in psEngine.RunAsync(
                PsCommandBuilder.Create()
                    .AddCommand("Remove-NetIPAddress")
                    .AddParameter("InterfaceAlias", adapterName)
                    .AddParameter("Confirm", false)
                    .AddParameter("ErrorAction", "SilentlyContinue")).ToAff()
            from uAddIp in psEngine.RunAsync(ipCommand).ToAff()
            select Unit.Default;


    }

    public Aff<RT, Seq<TypedPsObject<VMNetworkAdapter>>> GetNetAdaptersBySwitch(Guid switchId) =>
        default(RT).Powershell.Bind(ps => ps.GetObjectsAsync<VMNetworkAdapter>(
                PsCommandBuilder.Create().AddCommand("Get-VMNetworkAdapter")
                    .AddParameter("All")
                    .AddParameter("ErrorAction", "SilentlyContinue")).ToAff()
            .Map(s =>
                s.Where(a => !string.IsNullOrWhiteSpace(a.Value.VMName))
                    .Where(a => a.Value.SwitchId == switchId)));

    public Aff<RT, Unit> DisconnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters) =>
        default(RT).Powershell.Bind(ps =>

            adapters.Map(adapter =>
                    ps.RunAsync(
                            PsCommandBuilder.Create()
                                .AddCommand("Disconnect-VMNetworkAdapter")
                                .AddParameter("VMNetworkAdapter", adapter.PsObject)
                        )
                        .ToAsync()).TraverseParallel(l => l)
                .Map(_ => Unit.Default).ToAff());


    public Aff<RT,Unit> ReconnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters, string switchName) =>
        default(RT).Powershell.Bind(ps =>
            adapters.Map(adapter =>

                    ps.RunAsync(PsCommandBuilder.Create().AddCommand("Connect-VMNetworkAdapter")
                            .AddParameter("VMNetworkAdapter", adapter.PsObject)
                            .AddParameter("SwitchName", switchName)
                        )

                        .ToAsync()).TraverseParallel(l => l)
                .Map(_ => Unit.Default).ToAff());

    public Aff<RT,Unit> CreateOverlaySwitch(IEnumerable<string> adapters)
    {
        var createSwitchCommand = PsCommandBuilder.Create().AddCommand("new-VMSwitch")
            .AddParameter("Name", EryphConstants.OverlaySwitchName);

        var enumerable = adapters as string[] ?? adapters.ToArray();
        var createTeam = () => SuccessAff(Unit.Default);

        return default(RT).Powershell.Bind(ps =>
        {
            if (enumerable.Length() > 0)
            {
                string adapterName;
                if (enumerable.Length() > 1)
                {

                    // check if it necessary to enable AllowNetLbfoTeams option
                    // this is required for Windows Server 2022 and later
                    var newSwitchCommand = ps.GetObjects<PowershellCommand>(
                            new PsCommandBuilder().AddCommand("Get-Command").AddArgument("New-VMSwitch"))
                        .RightAsEnumerable().Flatten().AsEnumerable().FirstOrDefault();

                    if (newSwitchCommand is null)
                        return Prelude.FailAff<Unit>(Error.New("Cannot find command New-VMSwitch"));

                    if (newSwitchCommand.Value.Parameters.ContainsKey("AllowNetLbfoTeams"))
                        createSwitchCommand.AddParameter("AllowNetLbfoTeams", true);

                    createTeam = () => ps.RunAsync(
                            PsCommandBuilder.Create()
                                .AddCommand("New-NetSwitchTeam")
                                .AddParameter("Name", EryphConstants.OverlaySwitchName)
                                .AddParameter("TeamMembers", enumerable)

                            )
                        .ToAsync()
                        .ToAff(l => Error.New(l.Message));

                    adapterName = EryphConstants.OverlaySwitchName;
                }
                else
                    adapterName = enumerable[0];
                
                createSwitchCommand.AddParameter("NetAdapterName", adapterName)
                    .AddParameter("AllowManagementOS", false);
            }
            else
            {
                createSwitchCommand.AddParameter("SwitchType", "Private");

            }

            createSwitchCommand.AddCommand("Enable-VMSwitchExtension")
                .AddParameter("Name", "dbosoft Open vSwitch Extension");

            return
                createTeam().Bind(_ =>
                ps.RunAsync(createSwitchCommand)
                .ToAsync()
                .ToAff(l => Error.New(l.Message)));
        });
    }

    public Aff<RT, Option<OverlaySwitchInfo>> FindOverlaySwitch(
        Seq<VMSwitch> vmSwitches,
        Seq<VMSwitchExtension> extensions, 
        Seq<HostNetworkAdapter> adapters) =>

        default(RT).Powershell.Bind(ps => extensions
            .Find(x => x.Enabled)
            .Map(extension =>
            {
                var overlaySwitchId = extension.SwitchId;

                return vmSwitches
                    .Find(x => x.Id == overlaySwitchId)
                    .HeadOrLeft("Could not find overlay switch in switches even if extension was found")
                    .ToAff()
                    .Bind(vmSwitch =>
                    {
                        if (vmSwitch.NetAdapterInterfaceGuid is null or { Length: 0 })
                        {
                            return Prelude.SuccessAff(Seq<string>());
                        }

                        //even if it is a array only one adapter is supported in a switch
                        var switchAdapterGuid = vmSwitch.NetAdapterInterfaceGuid[0];

                        return adapters.Find(x =>
                                x.InterfaceGuid == switchAdapterGuid)
                            .Match(
                                Some: a => 
                                    Prelude.SuccessAff(new[]
                                    {
                                        a.Name
                                    }.ToSeq()),
                                None: () => ps.GetObjectsAsync<HostNetworkAdapter>(
                                        PsCommandBuilder.Create()
                                            .AddCommand("Get-NetSwitchTeamMember")
                                            .AddParameter("Team", EryphConstants.OverlaySwitchName))
                                    .ToAff()
                                    .Map(members =>
                                    {
                                        //assume if team exists that it contains only eryph adapters
                                        return members.Select(x => x.Value.Name);
                                    }));

                    }).Map(s => new OverlaySwitchInfo(
                        overlaySwitchId, Prelude.toHashSet(s)));

            }).Traverse(l => l));

    public Aff<RT,Unit> RemoveOverlaySwitch() =>
        from psEngine in default(RT).Powershell.ToAff()
        from uSwitch in psEngine.RunAsync(PsCommandBuilder.Create()
            .AddCommand("Remove-VMSwitch")
            .AddParameter("Name", EryphConstants.OverlaySwitchName)
            .AddParameter("Force")).ToAff()
        from uTeam in psEngine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Remove-NetSwitchTeam")
                .AddParameter("Name", EryphConstants.OverlaySwitchName)
                .AddParameter("ErrorAction", "SilentlyContinue"))
            .ToAff()
        select Unit.Default;

    public Aff<RT,Unit> RemoveNetNat(string natName) =>
        default(RT).Powershell.Bind(ps => ps.RunAsync(PsCommandBuilder.Create()
            .AddCommand("Remove-NetNat")
            .AddParameter("Name", natName)
            .AddParameter("Confirm", false)
        ).ToAff());

    public Aff<RT,Unit> AddNetNat(string natName, IPNetwork network) =>
        default(RT).Powershell.Bind(ps => ps.RunAsync(PsCommandBuilder.Create()
            .AddCommand("New-NetNat")
            .AddParameter("Name", natName)
            .AddParameter("InternalIPInterfaceAddressPrefix", network.ToString())

        ).ToAff());

    public Aff<RT,Seq<NetIpAddress>> GetAdapterIpV4Address(string adapterName)
    {
        return default(RT).Powershell.Bind(ps => ps.GetObjectsAsync<NetIpAddress>(
                PsCommandBuilder.Create().AddCommand("Get-NetIpAddress")
                    .AddParameter("InterfaceAlias", adapterName)
                    .AddParameter("AddressFamily", "IPv4")
                    .AddParameter("ErrorAction", "SilentlyContinue"))

            .MapAsync(e => e.Map(x => x.ToValue()))
            .ToAff());

    }

    public Aff<RT, Unit> WaitForBridgeAdapter(string bridgeName)
    {
        return default(RT).Powershell.Bind(ps =>
        {
            return Prelude.TryAsync(async () =>
                {
                    //make sure adapter is created
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(new TimeSpan(0, 0, 30));
                    while (!cts.IsCancellationRequested)
                    {
                        await Task.Delay(500, cts.Token);
                        var res = await ps.GetObjectsAsync<HostNetworkAdapter>(
                            PsCommandBuilder.Create().AddCommand("Get-NetAdapter")
                                .AddArgument(bridgeName));

                        if (res.IsLeft)
                        {
                            continue;
                        }

                        break;
                    }

                    await Task.Delay(1000, cts.Token); //relax a bit more
                    return Unit.Default;
                }).ToEither(f => Error.New(f.Message))
                .ToAff(f => Error.New($"Could not find adapter for bridge. Error: {f}"));

        });

    }
}