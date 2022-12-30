using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Mvc;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    internal class EntityOperationRequestHandler<TEntity> : IOperationRequestHandler<TEntity> where TEntity : class
    {
        private readonly IOperationDispatcher _operationDispatcher;
        private readonly IReadRepositoryBase<TEntity> _repository;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IMapper _mapper;
        private readonly IUserRightsProvider _userRightsProvider;

        public EntityOperationRequestHandler(IOperationDispatcher operationDispatcher, IReadRepositoryBase<TEntity> repository, IEndpointResolver endpointResolver, IMapper mapper, IUserRightsProvider userRightsProvider)
        {
            _operationDispatcher = operationDispatcher;
            _repository = repository;
            _endpointResolver = endpointResolver;
            _mapper = mapper;
            _userRightsProvider = userRightsProvider;
        }
        
        public async Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(Func<ISingleResultSpecification<TEntity>?> specificationFunc, Func<TEntity, object> createOperationFunc,
            CancellationToken cancellationToken)
        {
            var spec = specificationFunc();
            var model = spec != null ? await _repository.GetBySpecAsync(spec, cancellationToken) : null;

            if (model == null)
                return new NotFoundResult();

            if (model is Resource resource)
            {
                if(!(await _userRightsProvider.HasResourceAccess(resource.Id, AccessRight.Write)))
                    return new UnauthorizedResult();
            }

            if (model is Project project)
            {
                if (!(await _userRightsProvider.HasProjectAccess(project.Id, AccessRight.Admin)))
                    return new UnauthorizedResult();
            }


            var command = createOperationFunc(model);
            var operation = await _operationDispatcher.StartNew(_userRightsProvider.GetUserTenantId(),command);

            if(operation==null)
                return new UnprocessableEntityResult();

            var operationUri = new Uri(_endpointResolver.GetEndpoint("common") + $"/v1/operations/{operation.Id}");
            return new AcceptedResult(operationUri, new ListResponse<Operation>()){ Value = _mapper.Map<Operation>(operation)};
        }
    }
}