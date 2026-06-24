namespace Eryph.Modules.GenePool.Genetics;

public class GenePoolApiKey
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Organization { get; set; }

    public required string Secret { get; set; }
}
