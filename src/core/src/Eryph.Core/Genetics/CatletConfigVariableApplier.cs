using System.Collections.Generic;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Variables;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public static class CatletConfigVariableApplier
{
    public static Validation<Error, Seq<VariableConfig>> ApplyVariables(
        Seq<VariableConfig> configs,
        IReadOnlyDictionary<string, string> values) =>
        from _1 in Validations.ValidateDistinct(values.Keys, VariableName.NewValidation, "variable name")
        from valuesByName in values.ToSeq()
            .Map(kvp => from n in VariableName.NewValidation(kvp.Key)
                select (n, kvp.Value))
            .Sequence()
            .Map(r => r.ToHashMap())
        from names in configs
            .Map(c => VariableName.NewValidation(c.Name))
            .Sequence()
        from _2 in valuesByName.Keys.Except(names).ToSeq()
            .Map(n => Fail<Error, Unit>(Error.New($"Variable '{n}' is provided but not present in the configuration.")))
            .Sequence()
        from updatedConfigs in configs
            .Map(c => ApplyVariable(c, valuesByName))
            .Sequence()
        select updatedConfigs;

    private static Validation<Error, VariableConfig> ApplyVariable(
        VariableConfig config,
        HashMap<VariableName, string> valuesByName) =>
        from name in VariableName.NewValidation(config.Name)
        from value in valuesByName.Find(name).Match(
            Some: v => VariableConfigValidations.ValidateVariableValue(v, config.Type),
            None: () => Success<Error, string>(config.Value))
        select config.CloneWith(c =>
        {
            c.Value = value;
        });
}
