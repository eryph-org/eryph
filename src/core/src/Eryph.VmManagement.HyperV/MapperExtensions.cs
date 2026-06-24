using System;
using System.Linq;
using System.Reflection;
using AutoMapper;
using AutoMapper.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Management.Infrastructure;

namespace Eryph.VmManagement;

#pragma warning disable CS0168
public static class MapperExtensions
{
    extension(IProfileExpression profile)
    {
        public IProfileExpression AddCimInstanceMapping<TDestination>()
        {
            profile.CreateMap<CimInstance, TDestination>().ConvertUsing((source, _, context) =>
                context.Mapper.Map<TDestination>(
                    source.CimInstanceProperties.ToDictionary(p => p.Name, p => p.Value)));

            return profile;
        }

        public IProfileExpression AddHyperVMapping<TDestination>(Assembly assembly,
            string name, Action<IMappingExpression> configure = null)
        {
            var sourceType = assembly.GetType($"Microsoft.HyperV.PowerShell.{name}", false);
            if (sourceType == null) return profile;

            var destinationType = typeof(TDestination);

            var map = profile.CreateMap(sourceType, destinationType);
            configure?.Invoke(map);

            return profile;
        }
    }

    private static void IgnoreUnmappedProperties(TypeMap map, IMappingExpression expr, ILogger log)
    {
        foreach (var propName in map.GetUnmappedPropertyNames())
        {
            if (map.SourceType.GetProperty(propName) != null)
            {
                log.LogTrace(
                    "Powershell mapping: Ignoring source property {propName} on Type {type} (Assembly: {assembly} - missing in destination type {destType}",
                    propName,
                    map.SourceType.FullName, map.SourceType.AssemblyQualifiedName, map.DestinationType.FullName);
                expr.ForSourceMember(propName, opt => opt.DoNotValidate());
            }

            if (map.DestinationType.GetProperty(propName) == null) continue;

            log.LogTrace(
                "Powershell mapping: Ignoring destination property {propName} on Type {type} - missing in {sourceType} (Assembly: {assembly}",
                propName,
                map.DestinationType.FullName, map.SourceType.FullName, map.SourceType.AssemblyQualifiedName);
            expr.ForMember(propName, opt => opt.Ignore());
        }
    }

    extension(IProfileExpression profile)
    {
        public void IgnoreUnmapped(ILogger log)
        {
            profile.Internal().ForAllMaps((map, expr) => IgnoreUnmappedProperties(map, expr, log));
        }

        public void IgnoreUnmapped(Func<TypeMap, bool> filter, ILogger log)
        {
            profile.Internal().ForAllMaps((map, expr) =>
            {
                if (filter(map)) IgnoreUnmappedProperties(map, expr, log);
            });
        }

        public void IgnoreUnmapped(Type src, Type dest, ILogger log)
        {
            profile.IgnoreUnmapped(map => map.SourceType == src && map.DestinationType == dest, log);
        }
    }
}
