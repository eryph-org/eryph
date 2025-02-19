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

    public EitherAsync<Error, OVSTableRecord>  GetOVSTable(CancellationToken cancellationToken)
    {
        return GetRecord<OVSTableRecord>("open", ".", cancellationToken: cancellationToken);
    }

    public EitherAsync<Error, Unit> UpdateBridgeMapping(string bridgeMappings, CancellationToken cancellationToken)
    {

        return from ovsRecord in GetOVSTable(cancellationToken)

        let externalIds = ovsRecord.ExternalIds
            .Remove("ovn-bridge-mappings")
            .Add("ovn-bridge-mappings", bridgeMappings)

        from _ in UpdateRecord("open", ".",
            Map<string, IOVSField>(),
            new Map<string, IOVSField>(new[]
            {
                ("external_ids", (IOVSField)new OVSMap<string>(externalIds))
            }),
            Enumerable.Empty<string>(), cancellationToken)
        select Unit.Default;

    }

    public EitherAsync<Error, Unit> UpdateBridgePort(string bridgeName, int? tag, string? vlanMode, CancellationToken cancellationToken)
    {
        var columns = new Map<string, IOVSField>();
        if(tag > 0)
            columns = columns.Add("tag", new OVSValue<int>(tag.Value));
        if(vlanMode != null)
            columns = columns.Add("vlan_mode", new OVSValue<string>(vlanMode));

        var columnsToClear = new Lst<string>();
        if(tag.GetValueOrDefault() == 0)
            columnsToClear = columnsToClear.Add("tag");

        if (string.IsNullOrWhiteSpace(vlanMode))
            columnsToClear = columnsToClear.Add("vlan_mode");

        return from ovsRecord in GetOVSTable(cancellationToken)
            from _ in UpdateRecord("port", bridgeName,
                Map<string, IOVSField>(),columns,
                columnsToClear, cancellationToken)
            select Unit.Default;
    }

    public EitherAsync<Error, Unit> UpdateBondPort(
        string portName,
        string bondMode,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let columns = Map<string, IOVSField>(("bond_mode", new OVSValue<string>(bondMode)))
        let columnsToClear = new Lst<string>()
        from ovsRecord in GetOVSTable(cancellationToken)
        from _2 in UpdateRecord("port", portName,
            Map<string, IOVSField>(), columns,
            columnsToClear, cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> AddBridge(
        string bridgeName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommand($"--may-exist add-br \"{bridgeName}\"", false, cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> RemoveBridge(
        string bridgeName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommand($"--if-exists del-br \"{bridgeName}\"", false, cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> AddPort(
        string bridgeName,
        string portName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommand($"--may-exist add-port \"{bridgeName}\" \"{portName}\"", false, cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> AddPort(
        string bridgeName,
        string portName,
        Guid hostInterfaceId,
        string hostInterfaceConfiguredName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommand(
            $"--may-exist add-port \"{bridgeName}\" \"{portName}\""
            + $" -- set interface \"{portName}\" external_ids:host-iface-id={hostInterfaceId} external_ids:host-iface-confs-name={hostInterfaceConfiguredName}",
            false, cancellationToken)
        select unit;


    public EitherAsync<Error, Unit> AddPortWithIFaceId(
        string bridgeName,
        string portName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommand(
            $"--may-exist add-port \"{bridgeName}\" \"{portName}\""
            + $" -- set interface \"{portName}\" external_ids:iface-id={portName}",
            false, cancellationToken)
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
            + $" bond_mode={bondMode} other_config:bond-detect-mode=miimon"
            + string.Join("", interfaces.Map(i => $"--set interface \"{i.Name}\" external_ids:host-iface-id={i.InterfaceId} external_ids:host-iface-conf-name={i.ConfiguredName}"))
        from _2 in RunCommand(command, false, cancellationToken)
        select unit;

    public EitherAsync<Error, Unit> RemovePort(
        string bridgeName,
        string portName,
        CancellationToken cancellationToken) =>
        from _1 in RunCommand($" --if-exists del-port \"{bridgeName}\" \"{portName}\"", false, cancellationToken)
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
