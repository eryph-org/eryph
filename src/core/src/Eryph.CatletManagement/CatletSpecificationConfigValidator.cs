using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

using static Dbosoft.Functional.Validations.ComplexValidations;
using static LanguageExt.Prelude;

namespace Eryph.CatletManagement;

public static class CatletSpecificationConfigValidator
{
    public static Validation<ValidationIssue, Unit> Validate(
        CatletConfig catletConfig,
        string path = "") =>
        ValidateProperty(catletConfig, c => c.ConfigType, ValidateConfigType, path)
        | ValidateProperty(catletConfig, c => c.Name, CatletName.NewValidation, path, required: true)
        | ValidateProperty(catletConfig, c => c.Project, ValidateProjectName, path);

    private static Validation<Error, Unit> ValidateConfigType(CatletConfigType configType) =>
        guard(configType is CatletConfigType.Configuration,
                Error.New("The configuration must be a fresh configuration."))
            .ToValidation();

    private static Validation<Error, Unit> ValidateProjectName(string _) =>
        Fail<Error, Unit>(Error.New("The project must not be specified in the configuration."));
}
