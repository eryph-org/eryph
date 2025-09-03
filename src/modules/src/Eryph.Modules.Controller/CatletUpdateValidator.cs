using System;
using System.Linq.Expressions;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;
using Newtonsoft.Json;
using static Dbosoft.Functional.Validations.ComplexValidations;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller;

public static class CatletUpdateValidator
{
    public static Validation<ValidationIssue, Unit> Validate(
        CatletConfig updateConfig,
        CatletConfig originalConfig,
        Catlet catlet) =>
        ValidateProperty(updateConfig, c => c.ConfigType, ValidateConfigType, required: true)
        | ValidatePropertyValue<CatletConfig, ProjectName>(updateConfig, c => c.Project, catlet.Project.Name)
        | ValidatePropertyValue<CatletConfig, EnvironmentName>(updateConfig, c => c.Environment, catlet.Environment)
        | ValidatePropertyValue<CatletConfig, DataStoreName>(updateConfig, c => c.Store, catlet.DataStore)
        | ValidatePropertyValue<CatletConfig, StorageIdentifier>(updateConfig, c => c.Location, catlet.StorageIdentifier)
        | ValidatePropertyValue<CatletConfig, GeneSetIdentifier>(updateConfig, c => c.Parent, originalConfig.Parent)
        // TODO validate actual host name
        | ValidateProperty(updateConfig, c => c.Hostname, v => ValidateHostname(v, originalConfig.Hostname), required: true);
        
        // TODO validate actual host name
        // TODO Validate fodder and variables immutable
        // TODO Validate no new gene pool drives added

    private static Validation<Error, Unit> ValidateConfigType(CatletConfigType configType) =>
        guard(configType is CatletConfigType.Instance,
                Error.New("The configuration must be an instance configuration."))
            .ToValidation();

    private static Validation<ValidationIssue, Unit> ValidatePropertyValue<T, TValue>(
        T toValidate,
        Expression<Func<T, string?>> getProperty,
        string? expectedValue,
        string path = "")
        where TValue : EryphName<TValue> =>
        ValidateProperty(toValidate, getProperty, v => ValidateValue<TValue>(v, expectedValue), path, required: true);
    
    private static Validation<Error, string> ValidateValue<TValue>(
        string actualValue,
        string? expectedValue)
        where TValue : EryphName<TValue> =>
        from validExpectedValue in EryphName<TValue>.NewValidation(expectedValue)
            // Nested errors cannot be used here as we convert to ValidationIssue laters
            .MapFail(e => Error.New($"BUG! The expected value '{expectedValue}' is invalid: {e.Message}"))
        from validActualValue in EryphName<TValue>.NewValidation(actualValue)
        from _ in guard(validActualValue == validExpectedValue,
                Error.New("The value cannot be changed when updating an existing catlet."))
            .ToValidation()
        select actualValue;


    private static Validation<Error, string?> ValidateHostname(
        string? actualValue,
        string? expectedValue) =>
        from _ in guard(actualValue == expectedValue,
                Error.New("The hostname value cannot be changed when updating an existing catlet."))
            .ToValidation()
        select actualValue;
}
