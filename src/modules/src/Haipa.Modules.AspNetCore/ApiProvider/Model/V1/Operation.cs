using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.StateDb.Model;

namespace Haipa.Modules.AspNetCore.ApiProvider.Model.V1
{
    public class Operation
    {
        [Key] public string Id { get; set; } = null!;

        public OperationStatus Status { get; set; }

        public string? StatusMessage { get; set; }

        public IEnumerable<OperationResource>? Resources { get; set; }

        public IEnumerable<OperationLogEntry>? LogEntries { get; set; }
    }
}