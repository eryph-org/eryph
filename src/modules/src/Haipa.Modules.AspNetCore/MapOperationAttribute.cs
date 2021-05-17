using System;
using System.Threading.Tasks;
using AutoMapper;
using Haipa.StateDb.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Haipa.Modules.AspNetCore
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class MapOperationAttribute : ActionFilterAttribute
    {
        private IMapper _mapper;

        public override void OnActionExecuted(ActionExecutedContext actionExecutedContext)
        {
            _mapper = actionExecutedContext.HttpContext.RequestServices.GetRequiredService<IMapper>();

            if (!(actionExecutedContext.Result is AcceptedResult acceptedResult))
                return;

            Operation operation;

            switch (acceptedResult.Value)
            {
                case Task<Operation> operationTask:
                    operation = operationTask.GetAwaiter().GetResult();
                    break;
                case Operation modelOperation:
                    operation = modelOperation;
                    break;

                default: return;
            }


            var mappedOperation = _mapper.Map<ApiProvider.Model.V1.Operation>(operation);
            actionExecutedContext.Result = new AcceptedResult(acceptedResult.Location, mappedOperation);
        }
    }
}