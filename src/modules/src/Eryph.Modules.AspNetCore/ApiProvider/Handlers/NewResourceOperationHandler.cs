using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    class NewResourceOperationHandler<TModel> : INewResourceOperationHandler<TModel> where TModel : StateDb.Model.Resource
    {
        private readonly IOperationDispatcher _operationDispatcher;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IMapper _mapper;

        public NewResourceOperationHandler(IOperationDispatcher operationDispatcher, IEndpointResolver endpointResolver, IMapper mapper)
        {
            _operationDispatcher = operationDispatcher;
            _endpointResolver = endpointResolver;
            _mapper = mapper;
        }

        public async Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(Func<object> createOperationFunc,
            CancellationToken cancellationToken)
        {
            var command = createOperationFunc();
            var operation = await _operationDispatcher.StartNew(command);

            if (operation == null)
                return new UnprocessableEntityResult();

            var operationUri = new Uri(_endpointResolver.GetEndpoint("common") + $"/v1/operations/{operation.Id}");
            return new AcceptedResult(operationUri, new ListResponse<Operation>()) { Value = _mapper.Map<Operation>(operation) };
        }
    }
}