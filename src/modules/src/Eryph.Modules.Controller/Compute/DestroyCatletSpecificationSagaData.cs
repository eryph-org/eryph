using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.Modules.Controller.Compute;

internal class DestroyCatletSpecificationSagaData
{
    public DestroyCatletSpecificationSagaState State { get; set; }

    public Guid SpecificationId { get; set; }

    /// <summary>
    /// Every deployment of the specification. A specification is project level and deploys into many
    /// environments, so destroying it means destroying all of them, not just one.
    /// </summary>
    public Guid[] CatletIds { get; set; } = [];

    public List<Guid> CatletsDestroyed { get; set; } = [];

    public List<Resource> DestroyedResources { get; set; } = [];

    public List<Resource> DetachedResources { get; set; } = [];
}
