using Haipa.Primitives;
using Haipa.Primitives.Resources;

namespace Haipa.Messages.Resources
{
    public interface IResourcesCommand
    {
        Resource[] Resources { get; set; }
    }
}