using System;
using Eryph.Resources;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class OperationTaskReference
{
    public required string Id { get; set; }

    public required TaskReferenceType Type { get; set; }
    
    public required string ProjectName { get; set; }
}
