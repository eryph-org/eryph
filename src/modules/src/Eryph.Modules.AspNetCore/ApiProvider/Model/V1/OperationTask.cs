using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class OperationTask
{
    public required string Id { get; set; }

    public string? ParentTaskId { get; set; }

    public required string Name { get; set; }
    
    public string? DisplayName { get; set; }

    public required int Progress { get; set; }

    public required OperationTaskStatus Status { get; set; }
}
