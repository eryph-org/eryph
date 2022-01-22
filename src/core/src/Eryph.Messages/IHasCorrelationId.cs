using System;

namespace Eryph.Messages
{
    public interface IHasCorrelationId
    {
        Guid CorrelationId { get; set; }
    }
}