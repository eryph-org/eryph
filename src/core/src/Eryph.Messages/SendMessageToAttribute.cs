﻿using System;

namespace Eryph.Messages
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class SendMessageToAttribute : Attribute
    {
        public SendMessageToAttribute(MessageRecipient owner)
        {
            Recipient = owner;
        }

        public MessageRecipient Recipient { get; }
    }
}