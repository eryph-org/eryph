using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Mvc;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    internal class CreateEntityRequestHandler<TEntity> : ICreateEntityRequestHandler<TEntity>
    {
        private readonly IOperationDispatcher _operationDispatcher;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IMapper _mapper;
        private readonly IUserRightsProvider _userRightsProvider;

        public CreateEntityRequestHandler(IOperationDispatcher operationDispatcher, IEndpointResolver endpointResolver, IMapper mapper, IUserRightsProvider userRightsProvider)
        {
            _operationDispatcher = operationDispatcher;
            _endpointResolver = endpointResolver;
            _mapper = mapper;
            _userRightsProvider = userRightsProvider;
        }

        public async Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(Func<object> createOperationFunc,
            CancellationToken cancellationToken)
        {
            var command = createOperationFunc();
            var operation = await _operationDispatcher.StartNew(_userRightsProvider.GetUserTenantId(),command);

            if (operation == null)
                return new UnprocessableEntityResult();


            var operationUri = new Uri(_endpointResolver.GetEndpoint("common") + $"/v1/operations/{operation.Id}");
            return new AcceptedResult(operationUri, new ListResponse<Operation>()) { Value = _mapper.Map<Operation>(operation) };
        }
    }
}