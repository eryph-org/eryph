using System;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;

namespace Eryph.Core;

public static class AutoMapperExtensions
{
    public static IMappingExpression<TSource, TDestination> Flatten<TSource, TDestination, TSourceMember>(
        this IMappingExpression<TSource, TDestination> map,
        Expression<Func<TSource, TSourceMember>> source)
    {
        var innerSourceProperties =
            from sp in typeof(TSourceMember).GetProperties()
            where sp.CanRead
            join dp in typeof(TDestination).GetProperties()
                on sp.Name equals dp.Name
            where dp.CanWrite
            select sp;
        foreach (var innerSourceProperty in innerSourceProperties)
        {
            var innerProperty = Expression.Convert(Expression.Property(source.Body, innerSourceProperty), typeof(object));
            var mapFrom = Expression.Lambda<Func<TSource, object>>(innerProperty, source.Parameters);
            map.ForMember(innerSourceProperty.Name, c => c.MapFrom(mapFrom));
        }
        return map;
    }
}