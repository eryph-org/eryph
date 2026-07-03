using Eryph.ConfigModel.Variables;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletVariable
{
    public required string Name { get; set; }

    public VariableType Type { get; set; }

    public string? Value { get; set; }

    public bool Secret { get; set; }

    public bool Required { get; set; }
}
