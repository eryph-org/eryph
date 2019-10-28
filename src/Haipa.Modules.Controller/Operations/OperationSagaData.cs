using System;
using System.Collections.Generic;
using Rebus.Sagas;

namespace Haipa.Modules.Controller.Operations
{
    public class OperationSagaData : ISagaData
    {
        public Guid Id { get; set; }
        public int Revision { get; set; }

        public Guid OperationId { get; set; }
        public Guid PrimaryTaskId { get; set; }

        public Dictionary<Guid, string> Tasks { get; set; } = new Dictionary<Guid, string>();
    }
}