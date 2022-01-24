using System;

namespace Eryph.Messages.Resources.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class VerifyPlacementCalculationCommand
    {
        public Guid CorrelationId { get; set; }
    }
}