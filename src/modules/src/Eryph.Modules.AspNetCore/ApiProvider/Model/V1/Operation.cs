using System.Collections.Generic;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class Operation
{
    public required string Id { get; set; }

    public required OperationStatus Status { get; set; }

    public string? StatusMessage { get; set; }

    public IReadOnlyList<OperationResource>? Resources { get; set; }

    public IReadOnlyList<OperationLogEntry>? LogEntries { get; set; }

    public IReadOnlyList<Project>? Projects { get; set; }

    public IReadOnlyList<OperationTask>? Tasks { get; set; }

}
