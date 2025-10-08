using System;

namespace Eryph.Configuration.Model;

public class CatletSpecificationConfigModel
{
    public int Version { get; set; } = 1;

    public Guid ProjectId { get; set; }

    public string Name { get; set; } = null!;

    public Guid LatestId { get; set; }
}
