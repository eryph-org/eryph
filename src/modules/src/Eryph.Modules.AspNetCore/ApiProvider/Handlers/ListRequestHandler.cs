using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    internal class ListRequestHandler<TRequest, TResult, TModel>
        : IListRequestHandler<TRequest, TResult, TModel>
        where TModel : class
        where TRequest : IListRequest
    {
        private readonly IMapper _mapper;
        private readonly IReadRepositoryBase<TModel> _repository;

        public ListRequestHandler(IMapper mapper, IReadRepositoryBase<TModel> repository)
        {
            _mapper = mapper;
            _repository = repository;
        }

        public async Task<ActionResult<ListResponse<TResult>>> HandleListRequest(
            TRequest request,
            Func<TRequest, ISpecification<TModel>> createSpecificationFunc,
            CancellationToken cancellationToken)
        {
            var queryResult = await _repository.ListAsync(createSpecificationFunc(request), cancellationToken);
            var result = _mapper.Map<IEnumerable<TResult>>(queryResult);

            return new JsonResult(new ListResponse<TResult> { Value = result });
        }
    }
}