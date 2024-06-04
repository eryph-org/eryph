using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using static LanguageExt.Prelude;

namespace Eryph.Modules.AspNetCore;

public static class ValidationIssueExtensions
{
    /// <summary>
    /// Converts the given <see cref="Validation"/> to a
    /// <see cref="ModelStateDictionary"/> which contains the
    /// <see cref="ValidationIssue"/>s. The result can be used with
    /// <see cref="ControllerBase.ValidationProblem(ModelStateDictionary)"/>
    /// to return an error response with the validation issues.
    /// </summary>
    public static ModelStateDictionary ToModelStateDictionary<T>(
        this Validation<ValidationIssue, T> validation) =>
        validation.Match(
            Succ: _ => new ModelStateDictionary(),
            Fail: errors =>
            {
                var modelState = new ModelStateDictionary();
                
                foreach (var error in errors)
                {
                    modelState.AddModelError(error.Member, error.Message);
                }
                
                
                return modelState;
            });
}
