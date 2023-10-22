using System.ComponentModel.DataAnnotations;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class OperationTask
{
    [Key] public string Id { get; set; } = null!;

    public string? ParentTask { get; set; }
    public string? Name { get; set; }
    public string? DisplayName { get; set; }

    public int Progress { get; set; }

    public OperationTaskStatus Status { get; set; }
}
