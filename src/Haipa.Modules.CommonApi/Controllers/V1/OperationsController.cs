using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.AspNet.OData;
using Haipa.Modules.ApiProvider;
using Haipa.Modules.ApiProvider.Model;
using Haipa.Modules.ApiProvider.Model.V1;
using Haipa.Modules.CommonApi.Models.V1;
using Haipa.StateDb;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Haipa.Modules.CommonApi.Controllers.V1
{
    [ApiVersion( "1.0" )]
    [Authorize]
    [ApiExceptionFilter]
    public class OperationsController : ODataController
    {
        private readonly StateStoreContext _db;
        private readonly IMapper _mapper;

        public OperationsController(StateStoreContext context, IMapper mapper)
        {
            _db = context;
            _mapper = mapper;
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "Operations_List")]
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValueEx<IEnumerable<Operation>>))]
        [Produces("application/json")]
        public async Task<IActionResult> Get(ODataQueryOptions<Operation> options)
        {
            return Ok(await _db.Operations.GetAsync(_mapper, options));
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "Operations_Get")]
        [SwaggerResponse(Status200OK, "Success", typeof(Operation))]
        [Produces("application/json")]
        public async Task<IActionResult> Get(Guid key, ODataQueryOptions<Operation> options)
        {
            return Ok(SingleResult.Create(await _db.Operations.Where(x=>x.Id == key).GetQueryAsync(_mapper, options)));
        }


    }
}