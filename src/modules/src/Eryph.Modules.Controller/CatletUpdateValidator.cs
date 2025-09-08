using System;
using System.Linq.Expressions;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.ClassInstances;
using LanguageExt.Common;

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
        | ValidateProperty(updateConfig, c => c.Hostname, v => ValidateHostname(v, originalConfig.Hostname), required: true)
        | ValidateDrives(updateConfig, originalConfig)
        | ValidateProperty(updateConfig, c => c.Fodder,
            _ => Fail<Error, Unit>(Error.New("Fodder is not supported when updating an existing catlet.")))
        | ValidateProperty(updateConfig, c => c.Variables,
            _ => Fail<Error, Unit>(Error.New("Variables are not supported when updating an existing catlet.")));

    private static Validation<Error, Unit> ValidateConfigType(CatletConfigType configType) =>
        guard(configType is CatletConfigType.Instance,
                Error.New("The configuration must be an instance configuration."))
            .ToValidation();

    private static Validation<ValidationIssue, Unit> ValidateDrives(
        CatletConfig updateConfig,
        CatletConfig originalConfig) =>
        from _1 in Success<ValidationIssue, Unit>(unit)
        from originalGeneDrives in originalConfig.Drives.ToSeq()
            .Filter(d => Optional(d.Source).Map(s => s.StartsWith("gene:")).IfNone(false))
            .Map(d => from n in CatletDriveName.NewValidation(d.Name)
                      select (n, d))
            .Sequence()
            .Map(s => s.ToHashMap())
            .MapFail(e => new ValidationIssue("", $"BUG! The original config contains an invalid drive name: {e.Message}."))
        from _2 in ValidateList(updateConfig, c => c.Drives,
            (d,p) => ValidateDrive(d, originalGeneDrives, p))
        select unit;

    private static Validation<ValidationIssue, Unit> ValidateDrive(
        CatletDriveConfig updateConfig,
        HashMap<CatletDriveName, CatletDriveConfig> originalGeneDrives,
        string path = "") =>
        from _1 in ValidateProperty(updateConfig, d => d.Name, CatletDriveName.NewValidation, path, required: true)
        let name = CatletDriveName.New(updateConfig.Name)
        from _2 in originalGeneDrives.Find(name).Match(
            Some: originalConfig => ValidateDrive(updateConfig, originalConfig, path),
            None: () => ValidateDrive(updateConfig, path))
        select unit;

    private static Validation<ValidationIssue, Unit> ValidateDrive(
        CatletDriveConfig updateConfig,
        CatletDriveConfig originalConfig,
        string path = "") =>
        ValidateProperty<CatletDriveConfig, CatletDriveType, Unit>(
            updateConfig,
            d => d.Type,
            t => guard(
                    t == CatletDriveType.Vhd,
                    Error.New("The drive type of a gene pool drive cannot be changed when updating an existing catlet."))
                .ToValidation(),
            path)
        | ValidatePropertyValue<CatletDriveConfig, GeneIdentifier>(updateConfig, d => d.Source, originalConfig.Source,
            path);

    private static Validation<ValidationIssue, Unit> ValidateDrive(
        CatletDriveConfig updateConfig,
        string path = "") =>
        ValidateProperty(
            updateConfig,
            d => d.Source,
            s => guard(
                    s.StartsWith("gene:"),
                    Error.New("Cannot add new gene pool drives when updating an existing catlet."))
                .ToValidation(),
            path);

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
                Error.New("The hostname cannot be changed when updating an existing catlet."))
            .ToValidation()
        select actualValue;
}
