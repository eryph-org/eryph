using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.AspNet.OData;
using AutoMapper.Extensions.ExpressionMapping;
using AutoMapper.Internal;
using LogicBuilder.Expressions.Utils.Expansions;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Haipa.Modules.AspNetCore.OData
{
    public class MappedODataQueryOptions : ODataQueryOptions
    {
        private readonly ODataQueryOptions _options;
        private readonly IMapper _mapper;

        public MappedODataQueryOptions(ODataQueryOptions options, IMapper mapper) : base(options.Context, options.Request)
        {
            _options = options;
            _mapper = mapper;
        }

        public override IQueryable ApplyTo(IQueryable query, ODataQuerySettings querySettings)
        {
            if(!(query is IMappedResult mappedResult))
                throw new ArgumentException("The result content of an action with EnableMappedQueryAttribute has to be of type IMappedResult.");
            
            var entityQuery = mappedResult.EntityQueryable;
            var modelType = mappedResult.ModelType;
            var entityType = ElementTypeHelper.GetElementType(entityQuery.GetType());

            var genericMethod = this.GetType().GetMethod(nameof(ProjectToModel), BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Assert(genericMethod != null, nameof(genericMethod) + " != null");


            genericMethod = genericMethod.MakeGenericMethod(modelType, entityType);

            var mappedQueryable = genericMethod.Invoke(this, new[] { entityQuery, querySettings, mappedResult.Parameters }) as IQueryable;
            

            var result = _options.ApplyTo(mappedQueryable, querySettings);
            return result;
        }



        private IQueryable<TModel> ProjectToModel<TModel, TEntity>(IQueryable<TEntity> query, ODataQuerySettings querySettings, object parameters) where TModel : class
        {
            //currently it seems to be sufficient to use just ProjectTo - however see second method below is a option
            //when https://github.com/AutoMapper/AutoMapper.Extensions.OData is more compatible all odata features.

            var options = new Dictionary<string, object>();
            var mappedQueryable = _mapper.ProjectTo<TModel>(query, parameters);

            return mappedQueryable;
        }


        //private IQueryable<TModel> ProjectToModel<TModel, TEntity>(IQueryable<TEntity> query, ODataQuerySettings querySettings, object parameters) where TModel : class
        //{
        //    var options = new ODataQueryOptions<TModel>(_options.Context, _options.Request);

        //    var result = query.Get(_mapper, options);

        //    return result.AsQueryable();
        //}



    }

    public static class QueryableExtensions2
    {
        public static ICollection<TModel> Get<TModel, TData>(this IQueryable<TData> query, IMapper mapper, ODataQueryOptions<TModel> options, HandleNullPropagationOption handleNullPropagation = HandleNullPropagationOption.Default)
            where TModel : class
            => Task.Run(async () => await query.GetAsync(mapper, options, handleNullPropagation)).Result;

        public static async Task<ICollection<TModel>> GetAsync<TModel, TData>(this IQueryable<TData> query, IMapper mapper, ODataQueryOptions<TModel> options, HandleNullPropagationOption handleNullPropagation = HandleNullPropagationOption.Default)
            where TModel : class
        {
            ICollection<Expression<Func<IQueryable<TModel>, IIncludableQueryable<TModel, object>>>> includeExpressions = options.SelectExpand.GetIncludes().BuildIncludesExpressionCollection<TModel>()?.ToList();
            Expression<Func<TModel, bool>> filter = options.Filter.ToFilterExpression<TModel>(handleNullPropagation);
            Expression<Func<IQueryable<TModel>, IQueryable<TModel>>> queryableExpression = options.GetQueryableExpression();
            Expression<Func<IQueryable<TModel>, long>> countExpression = LinqExtensions.GetCountExpression<TModel>(filter);

            options.AddExpandOptionsResult();
            if (options.Count?.Value == true)
                options.AddCountOptionsResult<TModel, TData>(await query.QueryAsync(mapper, countExpression));

            return await query.GetAsync(mapper, filter, queryableExpression, includeExpressions);
        }

        //public static ICollection<TModel> Get<TModel, TData>(this IQueryable<TData> query, IMapper mapper,
        //    Expression<Func<TModel, bool>> filter = null,
        //    Expression<Func<IQueryable<TModel>, IQueryable<TModel>>> queryFunc = null,
        //    ICollection<Expression<Func<IQueryable<TModel>, IIncludableQueryable<TModel, object>>>> includeProperties = null)
        //    => Task.Run(async () => await query.GetAsync(mapper, filter, queryFunc, includeProperties)).Result;


        public static async Task<ICollection<TModel>> GetAsync<TModel, TData>(this IQueryable<TData> query, IMapper mapper,
            Expression<Func<TModel, bool>> filter = null,
            Expression<Func<IQueryable<TModel>, IQueryable<TModel>>> queryFunc = null,
            ICollection<Expression<Func<IQueryable<TModel>, IIncludableQueryable<TModel, object>>>> includeProperties = null)
        {
            //Map the expressions
            Expression<Func<TData, bool>> f = mapper.MapExpression<Expression<Func<TData, bool>>>(filter);
            Func<IQueryable<TData>, IQueryable<TData>> mappedQueryFunc = mapper.MapExpression<Expression<Func<IQueryable<TData>, IQueryable<TData>>>>(queryFunc)?.Compile();
            ICollection<Expression<Func<IQueryable<TData>, IIncludableQueryable<TData, object>>>> includes = mapper.MapIncludesList<Expression<Func<IQueryable<TData>, IIncludableQueryable<TData, object>>>>(includeProperties);

            if (filter != null)
                query = query.Where(f);

            if (includes != null)
                query = includes.Select(i => i.Compile()).Aggregate(query, (q, next) => q = next(q));

            //Call the store
            ICollection<TData> result = mappedQueryFunc != null ? await mappedQueryFunc(query).ToListAsync() : await query.ToListAsync();

            //Map and return the data
            return mapper.Map<IEnumerable<TData>, IEnumerable<TModel>>(result).ToList();
        }
    }


    internal static class ODataQueryOptionsExtensions
    {
        /// <summary>Adds the expand options to the result.</summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <param name="options">The odata options.</param>
        /// <autogeneratedoc />
        public static void AddExpandOptionsResult<TModel>(this ODataQueryOptions<TModel> options)
        {
            if (options.SelectExpand == null)
                return;

            options.Request.ODataFeature().SelectExpandClause = options.SelectExpand.SelectExpandClause;
        }

        /// <summary>Adds the count options to the result.</summary>
        /// <typeparam name="TModel">The type of the model.</typeparam>
        /// <typeparam name="TData">The type of the entity.</typeparam>
        /// <param name="options">The odata options.</param>
        /// <param name="query"></param>
        /// <autogeneratedoc />
        public static void AddCountOptionsResult<TModel, TData>(this ODataQueryOptions<TModel> options, long longCount)
        {
            if (options.Count?.Value != true)
                return;

            options.Request.ODataFeature().TotalCount = longCount;
        }
    }

}