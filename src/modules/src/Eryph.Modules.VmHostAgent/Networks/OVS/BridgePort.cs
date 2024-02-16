using System.Collections.Generic;
using Dbosoft.OVN.Model;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public record BridgePort : OVSTableRecord, IOVSEntityWithName
{
    public new static readonly IDictionary<string, OVSFieldMetadata>
        Columns = new Dictionary<string, OVSFieldMetadata>(OVSTableRecord.Columns)
        {
            { "name", OVSValue<string>.Metadata() },
            { "tag", OVSValue<int>.Metadata() },
            { "vlan_mode", OVSValue<string>.Metadata() },
        };

    public string Name => GetValue<string>("name");
    public int? Tag => GetValue<int>("tag");
    public string? VlanMode => GetValue<string>("vlan_mode");

}