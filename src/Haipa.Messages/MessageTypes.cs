using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Haipa.Messages
{
    public static class MessageTypes
    {
        public static IEnumerable<Type> BySubscribers(MessageSubscribers subscriber)
        {
            return MessageTypesWithAttributeValues<SubscribeEventAttribute>( a => a.Subscribers == subscriber);
        }

        public static IEnumerable<Type> ByOwner(MessageOwner owner)
        {
            return MessageTypesWithAttributeValues<MessageAttribute>(a => a.Owner == owner);
        }

        private static IEnumerable<Type> MessageTypesWithAttributeValues<TAttribute>(Func<TAttribute, bool> predicateFunc)
            where TAttribute : Attribute
        {
            return from type in GetTypesWithMessageAttribute()
                   from attribute in type.GetCustomAttributes<TAttribute>()
                   where predicateFunc(attribute)
                   select type;
                
        }

        private static IEnumerable<Type> GetTypesWithMessageAttribute()
        {
            var assembly = typeof(MessageAttribute).Assembly;

            return assembly.GetExportedTypes().Where(x =>
                x.GetCustomAttributes<MessageAttribute>().Any());
        }
    }

}
