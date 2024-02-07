using System;

namespace Eryph.Messages;

public interface IHasProjectId
{
    Guid ProjectId { get;  }
}

public interface IHasProjectName
{
    Guid TenantId { get;  }
    string ProjectName { get;  }
}