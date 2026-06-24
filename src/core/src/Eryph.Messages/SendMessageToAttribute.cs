using System;

namespace Eryph.Messages;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SendMessageToAttribute(MessageRecipient owner) : Attribute
{
    public MessageRecipient Recipient { get; } = owner;
}
