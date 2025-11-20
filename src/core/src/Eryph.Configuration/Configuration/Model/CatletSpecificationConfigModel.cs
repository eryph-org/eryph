using System;
using System.Collections.Generic;

namespace Eryph.Configuration.Model;

public class CatletSpecificationConfigModel
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Name { get; set; } = null!;

    public ISet<string> Architectures { get; set; } = null!;
}
