using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class RemoveSshKeyVMCommand : IVMCommand, IHasResource
    {
        public Guid CatletId { get; set; }
        public Guid VmId { get; set; }
        public string SubjectId { get; set; }
        public Resource Resource => new(ResourceType.Catlet, CatletId);
    }
}
