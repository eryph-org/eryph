using System;
using System.Diagnostics.CodeAnalysis;

namespace Eryph.Configuration.Model;

public class CatletSpecificationVersionConfigModel
{
    public Guid SpecificationId { get; set; }

    /// <summary>
    /// The ID of the catlet when this specification has been deployed.
    /// </summary>
    public Guid? CatletId { get; set; }

    public required string ConfigYaml { get; set; }

    [MaybeNull] public string Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
