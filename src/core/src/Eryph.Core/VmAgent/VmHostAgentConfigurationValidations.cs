using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

using static Dbosoft.Functional.Validations.ComplexValidations;
using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.Core.VmAgent;

public static class VmHostAgentConfigurationValidations
{
    public static Validation<ValidationIssue, Unit> ValidateVmHostAgentConfig(
        VmHostAgentConfiguration configuration,
        string path = "") =>
        from _ in ValidateProperty(configuration, c => c.Defaults, ValidateDefaultsConfig, path)
                  | ValidateList(configuration, c => c.Datastores, ValidateDataStoreConfig, path)
                  | ValidateList(configuration, c => c.Environments, ValidateEnvironmentConfig, path)
        from __ in ValidateNoDuplicatePaths(configuration)
                   | ValidateProperty(configuration, c => c.Datastores, ValidateNoDuplicateDataStores, path)
                   | ValidateProperty(configuration, c => c.Environments, ValidateNoDuplicateEnvironments, path)
        select unit;

    private static Validation<ValidationIssue, Unit> ValidateDataStoreConfig(
        VmHostAgentDataStoreConfiguration toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.Name, DataStoreName.NewValidation, path)
        | ValidateProperty(toValidate, c => c.Path, ValidatePath, path,
            required: true);

    private static Validation<ValidationIssue, Unit> ValidateDefaultsConfig(
        VmHostAgentDefaultsConfiguration toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.Vms, ValidatePath, path)
        | ValidateProperty(toValidate, c => c.Volumes, ValidatePath, path);

    private static Validation<ValidationIssue, Unit> ValidateEnvironmentConfig(
        VmHostAgentEnvironmentConfiguration toValidate,
        string path) =>
        from _ in ValidateProperty(toValidate, c => c.Name, EnvironmentName.NewValidation, path)
                  | ValidateProperty(toValidate, c => c.Defaults, ValidateEnvironmentDefaultsConfig, path,
                      required: true)
                  | ValidateList(toValidate, c => c.Datastores, ValidateDataStoreConfig, path)
        from __ in ValidateProperty(toValidate, c => c.Datastores, ValidateNoDuplicateDataStores, path)
        select unit;

    private static Validation<ValidationIssue, Unit> ValidateEnvironmentDefaultsConfig(
        VmHostAgentDefaultsConfiguration toValidate,
        string path) =>
        ValidateProperty(toValidate, c => c.Vms, ValidatePath, path, required: true)
        | ValidateProperty(toValidate, c => c.Volumes, ValidatePath, path, required: true);

    private static Validation<Error, string> ValidatePath(string path) =>
        from _  in Validations.ValidateWindowsPath(path, "value")
        // The general path validation uses OS-agnostic code. As we are on the
        // target system, we can validate with System.IO as well.
        from __ in Try(() => Path.IsPathFullyQualified(path))
            .ToOption()
            .Filter(v => v)
            .ToValidation(Error.New("The value must be a fully-qualified path."))
        select path;

    private static Validation<ValidationIssue, Unit> ValidateNoDuplicatePaths(
        VmHostAgentConfiguration toValidate) =>
        append(
            toValidate.Environments.ToSeq().Bind(e => append(
                Seq1(e.Defaults?.Vms),
                Seq1(e.Defaults?.Volumes),
                e.Datastores.ToSeq().Map(ds => ds.Path))
            ),
            toValidate.Datastores.ToSeq().Map(ds => ds.Path),
            Seq1(toValidate.Defaults?.Vms),
            Seq1(toValidate.Defaults?.Volumes)
        )
        .Filter(notEmpty)
        .Map(p => Path.TrimEndingDirectorySeparator(p).ToLowerInvariant())
        .GroupBy(identity)
        .Filter(g => g.Length() > 1)
        .Map(g => new ValidationIssue("", $"The path '{g.Key}' is not unique."))
        .Match(
            Empty: () => Success<ValidationIssue, Unit>(unit),
            More: Fail<ValidationIssue, Unit>);

    private static Validation<Error, Unit> ValidateNoDuplicateDataStores(
        VmHostAgentDataStoreConfiguration[] toValidate) =>
        toValidate.ToSeq()
            .Map(e => DataStoreName.New(e.Name))
            .GroupBy(n => n.Value)
            .Filter(g => g.Length() > 1)
            .Map(g => Error.New($"The data store '{g.Key}' is not unique."))
            .Match(
                Empty: () => Success<Error, Unit>(unit),
                More: Fail<Error, Unit>);

    private static Validation<Error, Unit> ValidateNoDuplicateEnvironments(
        VmHostAgentEnvironmentConfiguration[] toValidate) =>
        toValidate.ToSeq()
            .Map(e => EnvironmentName.New(e.Name))
            .GroupBy(n => n.Value)
            .Filter(g => g.Length() > 1)
            .Map(g => Error.New($"The environment '{g.Key}' is not unique."))
            .Match(
                Empty: () => Success<Error, Unit>(unit),
                More: Fail<Error, Unit>);
}
