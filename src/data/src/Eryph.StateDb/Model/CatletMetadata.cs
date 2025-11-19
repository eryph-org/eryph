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

    /// <summary>
    /// The ID of the specification (and the version via <see cref="SpecificationVersionId"/>)
    /// which was used to create this catlet.
    /// </summary>
    /// <remarks>
    /// This ID and <see cref="SpecificationVersionId"/> are soft-links and are not enforced
    /// via a foreign key constraint. It can happen that the corresponding specification
    /// does not exist.
    /// </remarks>
    public Guid? SpecificationId { get; set; }

    public Guid? SpecificationVersionId { get; set; }
}
