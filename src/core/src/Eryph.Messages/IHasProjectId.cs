using System;

namespace Eryph.Messages;

public interface IHasProjectId
{
    Guid ProjectId { get; set; }
}

public interface IHasProjectName
{
    Guid TenantId { get; set; }
    string ProjectName { get; set; }
}