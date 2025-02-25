using System.Collections.Generic;
using Dbosoft.OVN.Model;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public record OvsInterface : OVSTableRecord, IOVSEntityWithName
{
    public new static readonly IDictionary<string, OVSFieldMetadata>
        Columns = new Dictionary<string, OVSFieldMetadata>(OVSTableRecord.Columns)
        {
            { "name", OVSValue<string>.Metadata() },
            { "error", OVSValue<string>.Metadata() },
            { "link_state", OVSValue<string>.Metadata() },
            { "type", OVSValue<string>.Metadata() },
        };

    public string Name => GetValue<string>("name");

    public string? Error => GetValue<string?>("error");
    
    public string? LinkState => GetValue<string?>("link_state");

    public string Type => GetValue<string>("type");
}
