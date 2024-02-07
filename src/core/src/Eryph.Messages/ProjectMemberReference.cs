using System;
using Eryph.Resources;

namespace Eryph.Messages;

public class ProjectMemberReference : ITaskReference
{
    public TaskReferenceType ReferenceType => TaskReferenceType.ProjectMember;
    public string ReferenceId => AssignmentId.ToString();
    public string ProjectName { get; set; }

    public Guid ProjectId { get; set; }
    public Guid AssignmentId { get; set; }
}