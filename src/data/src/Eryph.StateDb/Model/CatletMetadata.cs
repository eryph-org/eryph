using System;
using System.Collections.Generic;
using Eryph.Resources.Machines;
using Eryph.Serializers;

// The change tracking in the controller module must be updated when modifying this entity.
namespace Eryph.StateDb.Model;

public class CatletMetadata
{
    public Guid Id { get; set; }

    public Guid CatletId { get; set; }

    public Guid VmId { get; set; }

    public bool SecretDataHidden { get; set; }

    public bool IsDeprecated { get; set; }

    internal string MetadataJson
    {
        get => Metadata is null ? "": CatletMetadataContentJsonSerializer.Serialize(Metadata);
        set => Metadata = string.IsNullOrEmpty(value) ? null : CatletMetadataContentJsonSerializer.Deserialize(value);
    }

    public CatletMetadataContent? Metadata { get; set; }

    public IList<CatletMetadataGene> Genes { get; set; } = null!;
}
