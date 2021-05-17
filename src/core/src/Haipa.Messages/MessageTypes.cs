using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Haipa.Messages
{
    public static class MessageTypes
    {
        public static IEnumerable<Type> BySubscriber(MessageSubscriber subscriber)
        {
            return MessageTypesWithAttributeValues<SubscribesMessageAttribute>(a => a.Subscriber == subscriber);
        }

        public static IEnumerable<Type> ByRecipient(MessageRecipient recipient)
        {
            return MessageTypesWithAttributeValues<SendMessageToAttribute>(a => a.Recipient == recipient);
        }

        private static IEnumerable<Type> MessageTypesWithAttributeValues<TAttribute>(
            Func<TAttribute, bool> predicateFunc)
            where TAttribute : Attribute
        {
            return from type in GetTypesWithMessageAttribute()
                from attribute in type.GetCustomAttributes<TAttribute>()
                where predicateFunc(attribute)
                select type;
        }

        private static IEnumerable<Type> GetTypesWithMessageAttribute()
        {
            var assembly = typeof(SendMessageToAttribute).Assembly;

            return assembly.GetExportedTypes().Where(x =>
                x.GetCustomAttributes<SendMessageToAttribute>().Any() ||
                x.GetCustomAttributes<SubscribesMessageAttribute>().Any());
        }
    }
}