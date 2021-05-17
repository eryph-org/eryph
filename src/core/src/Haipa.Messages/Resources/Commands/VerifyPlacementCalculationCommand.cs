using System;

namespace Haipa.Messages.Resources.Commands
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class VerifyPlacementCalculationCommand
    {
        public Guid CorrelationId { get; set; }
    }
}