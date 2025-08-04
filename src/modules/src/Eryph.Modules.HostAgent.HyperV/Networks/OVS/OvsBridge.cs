using System;
using System.Collections.Generic;
using Dbosoft.OVN.Model;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks.OVS;

public record OvsBridge : OVSTableRecord, IOVSEntityWithName
{
    public new static readonly IDictionary<string, OVSFieldMetadata>
        Columns = new Dictionary<string, OVSFieldMetadata>(OVSTableRecord.Columns)
        {
            { "name", OVSValue<string>.Metadata() },
            { "ports", OVSReference.Metadata() },
        };

    public string Name => GetValue<string>("name");

    public Seq<Guid> Ports => GetReference("ports");
}
