using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.VmConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Haipa.StateDb.Model
{
    public class Operation
    {
        public Guid Id { get; set; }
        public virtual List<OperationLogEntry> LogEntries { get; set; }
        public virtual List<OperationTask> Tasks { get; set; }
        public virtual List<OperationResource> Resources { get; set; }
        public OperationStatus Status { get; set; }

        public string StatusMessage { get; set; }
    }

    public class OperationResource
    {
        public Guid Id { get; set; }
        public long ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }

        public Operation Operation { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]

    public enum ComputeResourceType
    {
        Machine
    }
}