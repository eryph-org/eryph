using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dbosoft.OVN;
using Dbosoft.OVN.Model;
using Dbosoft.OVN.OSCommands.OVS;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public class OVSControl : OVSControlTool, IOVSControl
{
    private static readonly OvsDbConnection LocalOVSConnection
        = new(new OvsFile("/var/run/openvswitch", "db.sock"));

    public OVSControl([NotNull] ISysEnvironment sysEnv) : base(sysEnv, LocalOVSConnection)
    {

    }

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
            Map<string, IOVSField>.Empty,
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
                Map<string, IOVSField>.Empty,columns,
                columnsToClear, cancellationToken)
            select Unit.Default;

    }

    public EitherAsync<Error, Unit> AddBridge(string bridgeName, CancellationToken cancellationToken)
    {
        return RunCommand($" --may-exist add-br \"{bridgeName}\"", false, cancellationToken).Map(_ => Unit.Default);
    }

    public EitherAsync<Error, Unit> RemoveBridge(string bridgeName, CancellationToken cancellationToken)
    {
        return RunCommand($" --if-exists del-br \"{bridgeName}\"", false, cancellationToken).Map(_ => Unit.Default);
    }

    public EitherAsync<Error, Unit> AddPort(string bridgeName, string portName, CancellationToken cancellationToken)
    {
        return RunCommand($" --may-exist add-port \"{bridgeName}\" \"{portName}\"", false, cancellationToken).Map(_ => Unit.Default);
    }

    public EitherAsync<Error, Unit> AddPortWithIFaceId(string bridgeName, string portName, CancellationToken cancellationToken)
    {
        return RunCommand($" --may-exist add-port \"{bridgeName}\" \"{portName}\" -- set interface \"{portName}\" external_ids:iface-id={portName}", false, cancellationToken).Map(_ => Unit.Default);
    }

    public EitherAsync<Error, Unit> RemovePort(string bridgeName, string portName, CancellationToken cancellationToken)
    {
        return RunCommand($" --if-exists del-port \"{bridgeName}\" \"{portName}\"", false, cancellationToken).Map(_ => Unit.Default);
    }

    public EitherAsync<Error, Seq<Bridge>> GetBridges(CancellationToken cancellationToken)
    {
        return FindRecords<Bridge>("Bridge", Map<string, OVSQuery>.Empty, cancellationToken: cancellationToken);
    }

    public EitherAsync<Error, Interface> GetInterface(string interfaceName,
        CancellationToken cancellationToken = default)
    {
        return GetRecord<Interface>("Interface", interfaceName, cancellationToken: cancellationToken);
    }


    public EitherAsync<Error, Seq<BridgePort>> GetPorts(CancellationToken cancellationToken)
    {
        return FindRecords<BridgePort>("Port", Map<string, OVSQuery>.Empty, cancellationToken: cancellationToken);
    }
}