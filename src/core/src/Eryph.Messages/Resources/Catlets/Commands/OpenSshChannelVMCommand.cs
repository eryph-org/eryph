using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class OpenSshChannelVMCommand : IVMCommand, IHasResource
    {
        public Guid CatletId { get; set; }
        public Guid VmId { get; set; }

        // External-pool KVP values authorizing the operator's key (empty = none).
        public Dictionary<string, string> AccessKeyValues { get; set; }
        public Resource Resource => new(ResourceType.Catlet, CatletId);
    }
}
