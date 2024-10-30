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
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi;

public static class RequestValidations
{
    public static Validation<ValidationIssue, CatletConfig> ValidateCatletConfig(
        JsonElement jsonElement,
        string path) =>
        from config in Try(() => CatletConfigJsonSerializer.Deserialize(jsonElement))
            .ToValidation(ex => new ValidationIssue(path, $"The configuration is invalid: {ex.Message}"))
        from _ in CatletConfigValidations.ValidateCatletConfig(config, path)
        select config;

    public static Validation<ValidationIssue, ProjectNetworksConfig> ValidateProjectNetworkConfig(
        JsonElement jsonElement,
        string path) =>
        from config in Try(() => ProjectNetworksConfigJsonSerializer.Deserialize(jsonElement)).
            ToValidation(ex => new ValidationIssue(path, $"The configuration is invalid: {ex.Message}"))
        from _ in ComplexValidations.ValidateProperty(config, r => r.Project, ProjectName.NewValidation, path)
        select config;
}
