using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dbosoft.OVN;
using Dbosoft.OVN.Model;
using Dbosoft.OVN.OSCommands.OVS;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public class OVSControl(
    ISystemEnvironment systemEnvironment)
    : OVSControlTool(systemEnvironment, LocalOVSConnection), IOVSControl
{
    private static readonly OvsDbConnection LocalOVSConnection
        = new(new OvsFile("/var/run/openvswitch", "db.sock"));

    public EitherAsync<Error, OVSTableRecord> GetOVSTable(CancellationToken cancellationToken)
    {
        return GetRecord<OVSTableRecord>("open", ".", cancellationToken: cancellationToken);
    }

    public EitherAsync<Error, Unit> UpdateBridgeMapping(
        string bridgeMappings,
        CancellationToken cancellationToken) =>
        from ovsRecord in GetOVSTable(cancellationToken)
        let externalIds = ovsRecord.ExternalIds
            .Remove("ovn-bridge-mappings")
            .Add("ovn-bridge-mappings", bridgeMappings)
        from _ in UpdateRecord("open", ".",
            Map<string, IOVSField>(),
            Map<string, IOVSField>(("external_ids", OVSMap<string>.New(externalIds))),
            Seq<string>(),
            cancellationToken)
        select Unit.Default;

    public EitherAsync<Error, Unit> UpdateBridgePort(
        string bridgeName,
        Option<int> tag,
        Option<string> vlanMode,
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let columns = Seq(
                tag.Map(t => ("tag", (IOVSField)OVSValue<int>.New(t))),
                vlanMode.Map(v => ("vlan_mode", (IOVSField)OVSValue<string>.New(v))))
            .Somes().ToMap()
        let columnsToClear = Seq(
                Some("tag").Filter(_ => tag.IsNone),
                Some("vlan_mode").Filter(_ => vlanMode.IsNone))
            .Somes()
        from _2 in UpdateRecord("port", bridgeName,
            Map<string, IOVSField>(), columns, columnsToClear,
            cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> UpdateBondPort(
        string portName,
        string bondMode,
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let columns = Map<string, IOVSField>(("bond_mode", new OVSValue<string>(bondMode)))
        let columnsToClear = new Lst<string>()
        from _2 in UpdateRecord("port", portName,
            Map<string, IOVSField>(), columns, columnsToClear,
            cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> AddBridge(
        string bridgeName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommandWithResponse($"--may-exist add-br \"{bridgeName}\"", cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> RemoveBridge(
        string bridgeName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommandWithResponse($"--if-exists del-br \"{bridgeName}\"", cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> AddPort(
        string bridgeName,
        InterfaceUpdate @interface,
        CancellationToken cancellationToken) =>
        from _1 in RunCommandWithResponse(
            $"--may-exist add-port \"{bridgeName}\" \"{@interface.Name}\""
            + $" -- set interface \"{@interface.Name}\" external_ids:host-iface-id={@interface.InterfaceId} external_ids:host-iface-conf-name={@interface.ConfiguredName}",
            cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> AddPortWithIFaceId(
        string bridgeName,
        string portName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommandWithResponse(
            $"--may-exist add-port \"{bridgeName}\" \"{portName}\""
            + $" -- set interface \"{portName}\" external_ids:iface-id={portName}",
            cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> AddBond(
        string bridgeName,
        string portName,
        Seq<InterfaceUpdate> interfaces,
        string bondMode,
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let command = $"--may-exist add-bond \"{bridgeName}\" \"{portName}\""
            + $" {string.Join(" ", interfaces.Map(i => $"\"{i.Name}\""))}"
            + $" bond_mode={bondMode}"
            + string.Join("", interfaces.Map(i => $" -- set interface \"{i.Name}\" external_ids:host-iface-id={i.InterfaceId} external_ids:host-iface-conf-name={i.ConfiguredName}"))
        from _2 in RunCommandWithResponse(command, cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> RemovePort(
        string bridgeName,
        string portName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommandWithResponse($" --if-exists del-port \"{bridgeName}\" \"{portName}\"", cancellationToken)
        select unit;

    public EitherAsync<Error, Seq<Bridge>> GetBridges(
        CancellationToken cancellationToken) =>
        FindRecords<Bridge>("Bridge", Map<string, OVSQuery>(), cancellationToken: cancellationToken);

    public EitherAsync<Error, Interface> GetInterface(
        string interfaceName,
        CancellationToken cancellationToken = default) =>
        GetRecord<Interface>("Interface", interfaceName, cancellationToken: cancellationToken);

    public EitherAsync<Error, Seq<BridgePort>> GetPorts(
        CancellationToken cancellationToken) =>
        FindRecords<BridgePort>("Port", Map<string, OVSQuery>(), cancellationToken: cancellationToken);

    public EitherAsync<Error, Seq<Interface>> GetInterfaces(
        CancellationToken cancellationToken) =>
        FindRecords<Interface>("Interface", Map<string, OVSQuery>(), cancellationToken: cancellationToken);
}
