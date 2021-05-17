using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.StateDb.Model;
using Haipa.VmConfig;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;

namespace Haipa.Modules.AspNetCore.ApiProvider.Model.V1
{
    [Page(PageSize = 100)]
    public class Operation
    {
        [Key]
        public Guid Id { get; set; }
        public OperationStatus Status { get; set; }

        public string StatusMessage { get; set; }

        [Contained]
        [AutoExpand]
        public IEnumerable<OperationResource> Resources { get; set; }

        [Contained]
        [AutoExpand]

        public IEnumerable<OperationLogEntry> LogEntries { get; set; }

    }

    public class OperationResource
    {
        [Key]
        public Guid Id { get; set; }

        public string ResourceId { get; set; }
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