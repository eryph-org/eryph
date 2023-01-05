using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers
{
    internal class GetRequestHandler<TEntity, TResponse> : 
        IGetRequestHandler<TEntity, TResponse> where TEntity : class
    {
        private readonly IMapper _mapper;
        private readonly IReadRepositoryBase<TEntity> _repository;

        public GetRequestHandler(IMapper mapper, IReadRepositoryBase<TEntity> repository)
        {
            _mapper = mapper;
            _repository = repository;
        }

        public async Task<ActionResult<TResponse>> HandleGetRequest(
            Func<ISingleResultSpecification<TEntity>> specificationFunc, CancellationToken cancellationToken)
        {
            var result = await _repository.GetBySpecAsync(specificationFunc(), cancellationToken);

            if (result == null)
                return new NotFoundResult();

            var mappedResult = _mapper.Map<TResponse>(result);
            return new JsonResult(mappedResult);
        }

    }
}