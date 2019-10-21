using System;

namespace Haipa.Messages
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class MessageAttribute : Attribute
    {
        public MessageOwner Owner { get; }

        public MessageAttribute(MessageOwner owner)
        {
            Owner = owner;
        }
    }
}