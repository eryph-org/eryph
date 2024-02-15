using System;

namespace Eryph.Messages;

public interface IHasProjectName
{
    Guid TenantId { get;  }
    string ProjectName { get;  }
}