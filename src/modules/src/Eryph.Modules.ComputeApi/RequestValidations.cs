using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Model.V1;
using LanguageExt;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static LanguageExt.Prelude;
using ValidationIssue = Dbosoft.Functional.Validations.ValidationIssue;

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

    public static Validation<ValidationIssue, CatletConfig> ValidateCatletSpecificationConfig(
        CatletSpecificationConfig specificationConfig) =>
        from _1 in Success<ValidationIssue, Unit>(unit)
        let apiNamingPolicy = Optional(ApiJsonSerializerOptions.Options.PropertyNamingPolicy)
        from config in specificationConfig.ContentType switch
        {
            "application/json" => Try(() => CatletConfigJsonSerializer.Deserialize(specificationConfig.Content))
                .ToValidation(ToValidationIssue)
                .AddJsonPathPrefix(nameof(CatletSpecificationConfig.Content).ToJsonPath(apiNamingPolicy)),
            "application/yaml" => Try(() => CatletConfigYamlSerializer.Deserialize(specificationConfig.Content))
                .ToValidation(ToValidationIssue)
                .AddJsonPathPrefix(nameof(CatletSpecificationConfig.Content).ToJsonPath(apiNamingPolicy)),
            _ => new ValidationIssue(
                nameof(CatletSpecificationConfig.ContentType).ToJsonPath(apiNamingPolicy),
                $"The content type '{specificationConfig.ContentType}' is not supported.")
        }
        let configNamingPolicy = Optional(CatletConfigJsonSerializer.Options.PropertyNamingPolicy)
        from _2 in CatletConfigValidations.ValidateCatletConfig(config)
            .MapFail(vi => vi.ToJsonPath(configNamingPolicy))
            .AddJsonPathPrefix(nameof(CatletSpecificationConfig.Content).ToJsonPath(apiNamingPolicy))
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
