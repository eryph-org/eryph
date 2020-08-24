using System;
using System.Collections.Generic;
using System.Text;
using Haipa.Modules.ApiProvider.Model;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules
{
    public class ApiController : ODataController
    {

        /// <summary>
        /// Creates an <see cref="NotFoundObjectResult"/> that produces a <see cref="StatusCodes.Status404NotFound"/> response.
        /// </summary>
        /// <returns>The created <see cref="NotFoundObjectResult"/> for the response.</returns>
        [NonAction]
        public virtual NotFoundObjectResult NotFound(string message)
            => new NotFoundObjectResult(new ApiError(StatusCodes.Status404NotFound.ToString(), message));


    }
}
