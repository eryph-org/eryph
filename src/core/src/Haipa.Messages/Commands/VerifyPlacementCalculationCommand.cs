using System;

namespace Haipa.Messages.Operations
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class VerifyPlacementCalculationCommand
    {
        public Guid CorrelationId { get; set; }
    }
}