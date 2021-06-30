using System;
using System.ComponentModel.DataAnnotations;

namespace Haipa.Modules.AspNetCore.ApiProvider.Model.V1
{
    public class OperationLogEntry
    {
        [Key] public string Id { get; set; } = null!;

        public string? Message { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}