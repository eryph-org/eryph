using System;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class OperationResourceModel
{
    public Guid Id { get; set; }

    public Guid ResourceId { get; set; }
    
    public ResourceType ResourceType { get; set; }

    public virtual OperationModel Operation { get; set; } = null!;
}
