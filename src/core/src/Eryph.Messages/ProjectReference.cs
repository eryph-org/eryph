using System;
using Eryph.Resources;

namespace Eryph.Messages;

public class ProjectReference : ITaskReference
{
    public Guid ProjectId { get; set; }
    public TaskReferenceType ReferenceType => TaskReferenceType.Project;
    public string ReferenceId => ProjectId.ToString();
    public string? ProjectName { get; set; }
}
