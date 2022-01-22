using Eryph.Resources;

namespace Eryph.Messages.Resources.Commands
{
    public class DestroyResourcesResponse
    {
        public Resource[] DestroyedResources { get; set; }
        public Resource[] DetachedResources { get; set; }
    }
}