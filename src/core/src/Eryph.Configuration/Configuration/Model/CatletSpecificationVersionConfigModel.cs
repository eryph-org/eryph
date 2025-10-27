using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Eryph.Configuration.Model;

public class CatletSpecificationVersionConfigModel
{
    public Guid Id { get; set; }

    public Guid SpecificationId { get; set; }

    public ISet<string> Architectures { get; set; }

    public required string ContentType { get; set; }

    public required string OriginalConfig { get; set; }

    [MaybeNull] public string Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [MaybeNull] public IList<CatletSpecificationVersionVariantConfigModel> Variants { get; set; }
}
