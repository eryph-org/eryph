using System;

namespace Haipa.Messages
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class SubscribeEventAttribute : Attribute
    {
        public MessageSubscribers Subscribers { get; set; }

        public SubscribeEventAttribute(MessageSubscribers subscribers)
        {
            Subscribers = subscribers;
        }
    }
}