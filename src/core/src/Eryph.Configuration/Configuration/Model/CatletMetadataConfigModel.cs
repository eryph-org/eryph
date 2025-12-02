using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Eryph.Configuration.Model;

public class CatletMetadataConfigModel
{
    public int Version { get; set; } = 2;

    public Guid Id { get; set; }

    public Guid CatletId { get; set; }

    public Guid VmId { get; set; }

    public bool IsDeprecated { get; set; }

    public bool SecretDataHidden { get; set; }

    public JsonElement? Metadata { get; set; }

    public Guid? SpecificationId { get; set; }

    public Guid? SpecificationVersionId { get; set; }
}
