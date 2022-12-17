using System;

namespace Eryph.Messages;

public interface IHasProjectId
{
    Guid ProjectId { get; set; }
}