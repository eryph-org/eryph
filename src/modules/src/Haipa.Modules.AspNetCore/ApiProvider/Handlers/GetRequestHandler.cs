using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Haipa.Modules.AspNetCore.ApiProvider.Handlers
{
    internal class GetRequestHandler<TModel> : IGetRequestHandler<TModel> where TModel : class
    {
        private readonly IMapper _mapper;
        private readonly IReadRepositoryBase<TModel> _repository;

        public GetRequestHandler(IMapper mapper, IReadRepositoryBase<TModel> repository)
        {
            _mapper = mapper;
            _repository = repository;
        }

        public async Task<ActionResult<TResponse>> HandleGetRequest<TResponse>(
            Func<ISingleResultSpecification<TModel>> specificationFunc, CancellationToken cancellationToken)
        {
            var result = await _repository.GetBySpecAsync(specificationFunc(), cancellationToken);

            var mappedResult = _mapper.Map<TResponse>(result);
            return new JsonResult(mappedResult, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

    }
}