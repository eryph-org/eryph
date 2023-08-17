using System.Collections.Generic;
using Dbosoft.OVN.Model;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public record Interface : OVSTableRecord, IOVSEntityWithName
{
    public new static readonly IDictionary<string, OVSFieldMetadata>
        Columns = new Dictionary<string, OVSFieldMetadata>(OVSTableRecord.Columns)
        {
            { "name", OVSValue<string>.Metadata() },
            { "error", OVSSet<string>.Metadata() },
            { "link_state", OVSSet<string>.Metadata() },

        };

    public string Name => GetValue<string>("name");
    public Seq<string> Error => GetSet<string>("error");
    public Seq<string> LinkState => GetSet<string>("link_state");

}