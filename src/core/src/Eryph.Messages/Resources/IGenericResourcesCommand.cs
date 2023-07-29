using Eryph.Resources;

namespace Eryph.Messages.Resources;

public interface IGenericResourcesCommand
{
    Resource[] Resources { get; set; }
}