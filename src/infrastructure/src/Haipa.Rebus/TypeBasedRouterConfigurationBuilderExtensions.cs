using System;
using System.Collections.Generic;
using Rebus.Routing.TypeBased;

namespace Haipa.Rebus
{
    public static class TypeBasedRouterConfigurationBuilderExtensions
    {
        public static TypeBasedRouterConfigurationExtensions.TypeBasedRouterConfigurationBuilder Map(
            this TypeBasedRouterConfigurationExtensions.TypeBasedRouterConfigurationBuilder builder,
            IEnumerable<Type> types, string destination)
        {
            foreach (var type in types) builder.Map(type, destination);

            return builder;
        }
    }
}