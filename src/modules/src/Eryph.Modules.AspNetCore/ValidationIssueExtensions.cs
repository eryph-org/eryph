using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;
using static LanguageExt.Prelude;

namespace Eryph.Modules.AspNetCore;

public static class ValidationIssueExtensions
{
    public static ValidationProblemDetails ToProblemDetails<T>(
        this Validation<ValidationIssue, T> validation) =>
        validation.Match(
            Succ: _ => new ValidationProblemDetails(),
            Fail: errors => new ValidationProblemDetails(
                errors.ToLookup(e => e.Member, e => e.Message)
                    .ToDictionary(e => e.Key, e => e.ToArray())));
}
