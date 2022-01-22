using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    internal class ResourceOperationHandler<TModel> : IResourceOperationHandler<TModel> where TModel : StateDb.Model.Resource
    {
        private readonly IOperationDispatcher _operationDispatcher;
        private readonly IReadRepositoryBase<TModel> _repository;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IMapper _mapper;

        public ResourceOperationHandler(IOperationDispatcher operationDispatcher, IReadRepositoryBase<TModel> repository, IEndpointResolver endpointResolver, IMapper mapper)
        {
            _operationDispatcher = operationDispatcher;
            _repository = repository;
            _endpointResolver = endpointResolver;
            _mapper = mapper;
        }
        
        public async Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(Func<ISingleResultSpecification<TModel>> specificationFunc, Func<TModel, object> createOperationFunc,
            CancellationToken cancellationToken)
        {
            var model = await _repository.GetBySpecAsync(specificationFunc(), cancellationToken);

            if (model == null)
                return new NotFoundResult();

            var command = createOperationFunc(model);
            var operation = await _operationDispatcher.StartNew(command);

            if(operation==null)
                return new UnprocessableEntityResult();

            var operationUri = new Uri(_endpointResolver.GetEndpoint("common") + $"/v1/operations/{operation.Id}");
            return new AcceptedResult(operationUri, new ListResponse<Operation>()){ Value = _mapper.Map<Operation>(operation)};
        }
    }
}