using System;
using System.Collections.Generic;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations
{

    public class OperationSagaData : ISagaData
    {
        public Guid OperationId { get; set; }
        public Guid PrimaryTaskId { get; set; }

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
        public Dictionary<Guid, string> Tasks { get; set; } = new();
        public Guid Id { get; set; }
        public int Revision { get; set; }
    }
}