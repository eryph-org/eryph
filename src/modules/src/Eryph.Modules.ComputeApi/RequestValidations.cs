using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi;

public static class RequestValidations
{
    public static Validation<ValidationIssue, CatletConfig> ValidateCatletConfig(
        JsonElement jsonElement) =>
        from config in Try(() => CatletConfigJsonSerializer.Deserialize(jsonElement))
            .ToValidation(ToValidationIssue)
        let namingPolicy = Optional(CatletConfigJsonSerializer.Options.PropertyNamingPolicy)
        from _ in CatletConfigValidations.ValidateCatletConfig(config)
            .MapFail(vi => vi.ToJsonPath(namingPolicy))
        select config;

    public static Validation<ValidationIssue, ProjectNetworksConfig> ValidateProjectNetworkConfig(
        JsonElement jsonElement) =>
        from config in Try(() => ProjectNetworksConfigJsonSerializer.Deserialize(jsonElement)).
            ToValidation(ToValidationIssue)
        let namingPolicy = Optional(ProjectNetworksConfigJsonSerializer.Options.PropertyNamingPolicy)
        from _ in ComplexValidations.ValidateProperty(config, r => r.Project, ProjectName.NewValidation)
            .MapFail(vi => vi.ToJsonPath(namingPolicy))
        select config;

    private static ValidationIssue ToValidationIssue(
        Exception exception) =>
        exception switch
        {
            InvalidConfigException { InnerException: JsonException { Path: not null } jex } =>
                new ValidationIssue(jex.Path, jex.Message),
            _ => new ValidationIssue("$", $"The configuration is invalid: {exception.Message}")
        };
}
