using System;

namespace Eryph.Messages;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class SubscribesMessageAttribute(MessageSubscriber subscriber) : Attribute
{
    public MessageSubscriber Subscriber { get; set; } = subscriber;
}
