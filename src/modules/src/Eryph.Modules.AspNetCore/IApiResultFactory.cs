using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore;

public interface IApiResultFactory
{
    /// <summary>
    /// Creates a response with <see cref="ProblemDetails"/> in the same
    /// way as <see cref="ControllerBase.Problem"/> does.
    /// </summary>
    ObjectResult Problem(int statusCode, string detail);
}
