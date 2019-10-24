using System;

namespace Haipa.Messages
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class SendMessageToAttribute : Attribute
    {
        public MessageRecipient Recipient { get; }

        public SendMessageToAttribute(MessageRecipient owner)
        {
            Recipient = owner;
        }
    }
}