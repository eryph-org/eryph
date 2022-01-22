using System;

namespace Eryph.Messages
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class SubscribesMessageAttribute : Attribute
    {
        public SubscribesMessageAttribute(MessageSubscriber subscriber)
        {
            Subscriber = subscriber;
        }

        public MessageSubscriber Subscriber { get; set; }
    }
}