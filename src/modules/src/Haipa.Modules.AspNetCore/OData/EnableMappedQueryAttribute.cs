using System;
using System.Linq;
using AutoMapper;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Haipa.Modules.AspNetCore.OData
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class EnableMappedQueryAttribute : EnableQueryAttribute
    {
        private IMapper _mapper;

        public override void OnActionExecuted(ActionExecutedContext actionExecutedContext)
        {
            _mapper = actionExecutedContext.HttpContext.RequestServices.GetRequiredService<IMapper>();
            base.OnActionExecuted(actionExecutedContext);
        }

        public override IQueryable ApplyQuery(IQueryable queryable, ODataQueryOptions queryOptions)
        {
            var mappedOptions = new MappedODataQueryOptions(queryOptions, _mapper);
            return base.ApplyQuery(queryable, mappedOptions);
        }
    }
}