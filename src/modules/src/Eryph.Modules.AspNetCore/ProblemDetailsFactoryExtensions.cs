using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

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
