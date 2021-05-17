using Haipa.Primitives;
using Haipa.Primitives.Resources;

namespace Haipa.Messages.Resources
{
    public interface IResourceCommand
    {
        Resource Resource { get; set; }
    }
}