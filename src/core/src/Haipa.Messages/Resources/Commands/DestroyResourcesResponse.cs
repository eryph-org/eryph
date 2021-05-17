using Haipa.Primitives;
using Haipa.Primitives.Resources;

namespace Haipa.Messages.Resources.Commands
{
    public class DestroyResourcesResponse
    {
        public Resource[] DestroyedResources { get; set; }
        public Resource[] DetachedResources { get; set; }
    }
}