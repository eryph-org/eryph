using System;

namespace Haipa.Messages
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class SubscribesMessageAttribute : Attribute
    {
        public MessageSubscriber Subscriber { get; set; }

        public SubscribesMessageAttribute(MessageSubscriber subscriber)
        {
            Subscriber = subscriber;
        }
    }
}