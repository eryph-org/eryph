using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Commands;

public class DestroyResourcesResponse
{
    public required IReadOnlyList<Resource> DestroyedResources { get; set; }
    
    public required IReadOnlyList<Resource> DetachedResources { get; set; }
}
