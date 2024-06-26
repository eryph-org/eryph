﻿using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using JetBrains.Annotations;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints
{
    public abstract class ListEntityEndpoint<TRequest, TResult, TEntity> : ListEndpoint<TRequest, TResult, TEntity> 
        where TRequest : IListRequest
        where TEntity : class
    {
        private readonly IListEntitySpecBuilder<TRequest,TEntity> _specBuilder;

        protected ListEntityEndpoint(
            IListRequestHandler<TRequest, TResult, TEntity> listRequestHandler,
            IListEntitySpecBuilder<TRequest,TEntity> specBuilder)
            : base(listRequestHandler)
        {
            _specBuilder = specBuilder;
        }

        protected override ISpecification<TEntity> CreateSpecification(TRequest request)
        {
            return _specBuilder.GetEntitiesSpec(request);
        }
    }

}