using System;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class OperationLogEntry
{
    public required string Id { get; set; }

    public required string TaskId { get; set; }

    public string? Message { get; set; }

    public required DateTimeOffset Timestamp { get; set; }
}
