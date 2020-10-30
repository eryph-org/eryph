using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Haipa.Modules.AspNetCore
{

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]

    public class MapOperationAttribute : ActionFilterAttribute
    {
        private IMapper _mapper;

        public override void OnActionExecuted(ActionExecutedContext actionExecutedContext)
        {
            _mapper = actionExecutedContext.HttpContext.RequestServices.GetRequiredService<IMapper>();

            if (!(actionExecutedContext.Result is AcceptedResult acceptedResult))
                return;

            StateDb.Model.Operation operation;

            switch (acceptedResult.Value)
            {
                case Task<StateDb.Model.Operation> operationTask:
                    operation = operationTask.GetAwaiter().GetResult();
                    break;
                case StateDb.Model.Operation modelOperation:
                    operation = modelOperation;
                    break;

                default: return;
            }


            var mappedOperation = _mapper.Map<Operation>(operation);
            actionExecutedContext.Result = new AcceptedResult(acceptedResult.Location, mappedOperation);

        }

    }
}
