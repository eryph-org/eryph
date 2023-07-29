using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Rebus.TransactionScopes;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    internal class CreateEntityRequestHandler<TEntity> : ICreateEntityRequestHandler<TEntity>
    {
        private readonly IOperationDispatcher _operationDispatcher;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IMapper _mapper;
        private readonly IUserRightsProvider _userRightsProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly StateStoreContext _dbContext;


        public CreateEntityRequestHandler(IOperationDispatcher operationDispatcher, IEndpointResolver endpointResolver, IMapper mapper, IUserRightsProvider userRightsProvider, IHttpContextAccessor httpContextAccessor, StateStoreContext dbContext)
        {
            _operationDispatcher = operationDispatcher;
            _endpointResolver = endpointResolver;
            _mapper = mapper;
            _userRightsProvider = userRightsProvider;
            _httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
        }

        public async Task<ActionResult<ListResponse<Operation>>> HandleOperationRequest(Func<object> createOperationFunc,
            CancellationToken cancellationToken)
        {
            using var ta = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
            ta.EnlistRebus();

            var command = createOperationFunc();
            var operation = await _operationDispatcher.StartNew(_userRightsProvider.GetUserTenantId(), _httpContextAccessor.HttpContext?.TraceIdentifier??"",  command);
            var operationModel = (operation as StateDb.Workflows.Operation)?.Model;

            if (operationModel == null)
                return new UnprocessableEntityResult();

            var mappedModel = _mapper.Map<Operation>(operationModel);
            var operationUri = new Uri(_endpointResolver.GetEndpoint("common") + $"/v1/operations/{operationModel.Id}");

            await _dbContext.SaveChangesAsync(cancellationToken);
            ta.Complete();

            return new AcceptedResult(operationUri, new ListResponse<Operation>()) { Value = mappedModel };
        }
    }
}