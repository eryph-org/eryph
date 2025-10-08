using System;

namespace Eryph.Configuration.Model;

public class CatletSpecificationVersionConfigModel
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// The ID of the catlet when this specification has been deployed.
    /// </summary>
    public Guid? CatletId { get; set; }

    public required string ConfigYaml { get; set; }

    public bool IsDraft { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
