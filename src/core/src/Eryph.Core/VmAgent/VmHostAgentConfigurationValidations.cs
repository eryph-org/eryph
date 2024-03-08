using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

using static Dbosoft.Functional.Validations.ComplexValidations;
using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.Core.VmAgent
{
    public static class VmHostAgentConfigurationValidations
    {
        public static Validation<ValidationIssue, Unit> ValidateVmHostAgentConfig(
            VmHostAgentConfiguration configuration,
            string path = "") =>
            from _ in ValidateProperty(configuration, c => c.Defaults, ValidateDefaultsConfig, path, required: true)
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
            | ValidateProperty(toValidate, c => c.Path, p => Validations.ValidatePath(p, "value"), path,
                required: true);

        private static Validation<ValidationIssue, Unit> ValidateDefaultsConfig(
            VmHostAgentDefaultsConfiguration toValidate,
            string path) =>
            ValidateProperty(toValidate, c => c.Vms, p => Validations.ValidatePath(p, "value"), path)
            | ValidateProperty(toValidate, c => c.Volumes, p => Validations.ValidatePath(p, "value"), path);

        private static Validation<ValidationIssue, Unit> ValidateEnvironmentConfig(
            VmHostAgentEnvironmentConfiguration toValidate,
            string path) =>
            from _ in ValidateProperty(toValidate, c => c.Name, EnvironmentName.NewValidation, path)
                      | ValidateList(toValidate, c => c.Datastores, ValidateDataStoreConfig, path)
                      | ValidateProperty(toValidate, c => c.Defaults, ValidateEnvironmentDefaultsConfig, path,
                          required: true)
            from __ in ValidateProperty(toValidate, c => c.Datastores, ValidateNoDuplicateDataStores, path)
            select unit;

        private static Validation<ValidationIssue, Unit> ValidateEnvironmentDefaultsConfig(
            VmHostAgentDefaultsConfiguration toValidate,
            string path) =>
            ValidateProperty(toValidate, c => c.Vms, p => Validations.ValidatePath(p, "value"), path, required: true)
            | ValidateProperty(toValidate, c => c.Volumes, p => Validations.ValidatePath(p, "value"), path, required: true);

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
            ).Filter(notEmpty)
            .GroupBy(identity)
            .Filter(g => g.Length() > 1)
            .Map(g => new ValidationIssue("", $"The path '{g.Key}' is not unique."))
            .Match(
                Empty: () => Success<ValidationIssue, Unit>(unit),
                More: Fail<ValidationIssue, Unit>);

        private static Validation<Error, Unit> ValidateNoDuplicateDataStores(
            VmHostAgentDataStoreConfiguration[]? toValidate) =>
            toValidate.ToSeq()
                .Map(e => e.Name)
                .GroupBy(identity)
                .Filter(g => g.Length() > 1)
                .Map(g => Error.New($"The data store '${g.Key}' is not unique."))
                .Match(
                    Empty: () => Success<Error, Unit>(unit),
                    More: Fail<Error, Unit>);

        private static Validation<Error, Unit> ValidateNoDuplicateEnvironments(
            VmHostAgentEnvironmentConfiguration[]? toValidate) =>
            toValidate.ToSeq()
                .Map(e => e.Name)
                .GroupBy(identity)
                .Filter(g => g.Length() > 1)
                .Map(g => Error.New($"The environment '${g.Key}' is not unique."))
                .Match(
                    Empty: () => Success<Error, Unit>(unit),
                    More: Fail<Error, Unit>);
    }
}
