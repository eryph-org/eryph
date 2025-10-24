using Eryph.Serializers;
using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class CatletSpecificationVersion
{
    public Guid Id { get; set; }

    public Guid SpecificationId { get; set; }

    /// <summary>
    /// The content type of the <see cref="Configuration"/>.
    /// Can be <c>application/json</c> or <c>application/yaml</c>.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// The original configuration as provided by the user.
    /// Refer to <see cref="ContentType"/> for the format.
    /// </summary>
    public required string Configuration { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
