using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.AspNetCore
{
    [MapOperation]
    [ApiExceptionFilter]
    public class ApiController : ODataController
    {
        /// <summary>
        ///     Creates an <see cref="NotFoundObjectResult" /> that produces a <see cref="StatusCodes.Status404NotFound" />
        ///     response.
        /// </summary>
        /// <returns>The created <see cref="NotFoundObjectResult" /> for the response.</returns>
        [NonAction]
        public new NotFoundObjectResult NotFound(string message)
        {
            return new NotFoundObjectResult(new ApiError(StatusCodes.Status404NotFound.ToString(), message));
        }
    }
}