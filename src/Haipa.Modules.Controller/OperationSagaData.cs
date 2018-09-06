using System;
using Rebus.Sagas;

namespace Haipa.Modules.Controller
{
    public class OperationSagaData : ISagaData
    {
        public Guid Id { get; set; }
        public int Revision { get; set; }

        public Guid OperationId { get; set; }

    }
}