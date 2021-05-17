using System;

namespace Haipa.Messages
{
    public interface IHasCorrelationId
    {
        Guid CorrelationId { get; set; }

    }
}