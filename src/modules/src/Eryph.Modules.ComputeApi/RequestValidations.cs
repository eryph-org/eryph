using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using JetBrains.Annotations;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi;

public static class RequestValidations
{
    public static Validation<ValidationIssue, CatletConfig> ValidateCatletConfig(
        JsonElement jsonElement,
        string path) =>
        from configDictionary in TryOption(() => ConfigModelJsonSerializer.DeserializeToDictionary(jsonElement))
            .ToValidation(ex => new ValidationIssue(path, ex.Message))
        from validConfigDictionary in configDictionary
            .ToValidation(CreateMissingConfigIssue(path))
        from config in TryOption(() => CatletConfigDictionaryConverter.Convert(validConfigDictionary))
            .ToValidation(ex => new ValidationIssue(path, ex.Message))
        from validConfig in config
            .ToValidation(CreateMissingConfigIssue(path))
        from _ in CatletConfigValidations.ValidateCatletConfig(validConfig, path)
        select validConfig;

    public static Validation<ValidationIssue, ProjectNetworksConfig> ValidateProjectNetworkConfig(
        JsonElement jsonElement,
        string path) =>
        from configDictionary in TryOption(() => ConfigModelJsonSerializer.DeserializeToDictionary(jsonElement))
            .ToValidation(ex => new ValidationIssue(path, ex.Message))
        from validConfigDictionary in configDictionary
            .ToValidation(CreateMissingConfigIssue(path))
        from config in TryOption(() => ProjectNetworksConfigDictionaryConverter.Convert(validConfigDictionary))
            .ToValidation(ex => new ValidationIssue(path, ex.Message))
        from validConfig in config
            .ToValidation(CreateMissingConfigIssue(path))
        from _ in ComplexValidations.ValidateProperty(validConfig, r => r.Project, ProjectName.NewValidation, path)
        select validConfig;

    private static ValidationIssue CreateMissingConfigIssue(string path) =>
        new (path, "The configuration is missing.");
}