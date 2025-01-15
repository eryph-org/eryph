using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using static LanguageExt.Prelude;

namespace Eryph.Modules.AspNetCore;

public static class ProblemDetailsFactoryExtensions
{
    public static ProblemDetails CreateForbiddenProblemDetails(
        this ProblemDetailsFactory factory,
        HttpContext httpContext,
        AuthorizationResult authorizationResult)
    {
        var messages = Optional(authorizationResult.Failure).ToSeq()
            .Bind(f => f.FailureReasons.ToSeq())
            .Map(r => r.Message)
            .Filter(notEmpty);

        var detail = string.Join(Environment.NewLine, messages);

        var problemDetails = factory.CreateProblemDetails(
            httpContext,
            statusCode: StatusCodes.Status403Forbidden,
            detail: notEmpty(detail) ? detail : null);

        return problemDetails;
    }

    /// <summary>
    /// Create an <see cref="ActionResult"/> with <see cref="ProblemDetails"/>
    /// for the given <paramref name="authorizationResult"/>.
    /// </summary>
    /// <remarks>
    /// This method is similar to
    /// <see cref="ControllerBase.ValidationProblem(ModelStateDictionary)"/>.
    /// There is no built-in method in ASP.NET Core to create an <see cref="ActionResult"/>
    /// from an <see cref="AuthorizationResult"/>.
    /// </remarks>
    public static ActionResult CreateForbiddenResult(
        this ProblemDetailsFactory factory,
        HttpContext httpContext,
        AuthorizationResult authorizationResult)
    {
        var problemDetails = factory.CreateForbiddenProblemDetails(
            httpContext,
            authorizationResult);

        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status,
        };
    }
}
