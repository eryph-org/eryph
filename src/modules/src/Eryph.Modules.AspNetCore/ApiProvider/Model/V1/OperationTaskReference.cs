using System;
using Eryph.Resources;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class OperationTaskReference
{
    public string? Id { get; set; }
    public TaskReferenceType? Type { get; set; }
    public string? ProjectName { get; set; }

}