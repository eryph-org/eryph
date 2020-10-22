using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.StateDb.Model;
using Microsoft.AspNet.OData.Builder;

namespace Haipa.Modules.ApiProvider.Model.V1
{
    public class Operation
    {
        [Key]
        public Guid Id { get; set; }
        public OperationStatus Status { get; set; }

        public string StatusMessage { get; set; }

        [Contained]
        public IEnumerable<OperationResource> Resources { get; set; }

        [Contained]
        public IEnumerable<OperationLogEntry> LogEntries { get; set; }

    }

    public class OperationResource
    {
        [Key]
        public Guid Id { get; set; }

        public Guid ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }
    }

    public class OperationLogEntry
    {
        [Key]
        public Guid Id { get; set; }

        public string Message { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}