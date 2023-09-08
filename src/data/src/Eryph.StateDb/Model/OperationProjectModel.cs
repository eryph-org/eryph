using System;

namespace Eryph.StateDb.Model;

public class OperationProjectModel
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project{ get; set; }

    public virtual OperationModel Operation { get; set; }
}