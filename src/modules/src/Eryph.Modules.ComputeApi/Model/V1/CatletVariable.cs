using Eryph.ConfigModel.Variables;

namespace Eryph.Modules.ComputeApi.Model.V1;

// Mirrors the nullable shape of the source variable definition. This is an
// external contract, so the properties stay nullable and no defaults are
// invented here - the values are reported exactly as they are in the built
// config, and consumers apply their own defaults.
public class CatletVariable
{
    public string? Name { get; set; }

    public VariableType? Type { get; set; }

    public string? Value { get; set; }

    public bool? Secret { get; set; }

    public bool? Required { get; set; }
}
